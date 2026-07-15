using System.Security.Cryptography;
using Application.DTOs.RequestDTOs.Auth;
using Application.DTOs.ResponseDTOs.Auth;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Account;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    // Register/Login theo idiom ChemXLab (BCrypt + ApiExceptionResponse) + refresh token xoay vòng.
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService, IConfiguration configuration, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<AuthResponseDTO> Register(RegisterDTO request)
        {
            // Kiểm tra email đã tồn tại
            var existing = await _unitOfWork.AccountRepository.GetByEmailAsync(request.Email);
            if (existing != null)
            {
                // Nếu tài khoản đang PendingVerification thì cho phép gửi lại OTP
                if (existing.Status == AccountStatus.PendingVerification)
                    throw new ApiExceptionResponse(
                        "Email đã được đăng ký nhưng chưa xác thực. Vui lòng kiểm tra email hoặc yêu cầu gửi lại mã OTP.", 409);
                throw new ApiExceptionResponse("Email already exists.", 409);
            }

            var otp = GenerateOtp();
            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserName = request.UserName,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = AccountRole.User,
                Status = AccountStatus.PendingVerification,
                IsEmailVerified = false,
                EmailOtp = otp,
                EmailOtpExpiresAt = DateTime.UtcNow.AddMinutes(3),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.AccountRepository.CreateAsync(account);
            await _unitOfWork.CommitAsync();

            // Gửi OTP ngay lập tức (không qua digest)
            await _emailService.SendEmailAsync(
                account.Email,
                "Xác thực email đăng ký - BIM CDE Portal",
                $"Xin chào {account.UserName},\n\n" +
                $"Mã xác thực (OTP) của bạn là: {otp}\n\n" +
                $"Mã này có hiệu lực trong 3 phút.\n" +
                $"Nếu bạn không yêu cầu đăng ký, hãy bỏ qua email này.");

            // Trả về response tạm (không có token vì chưa xác thực)
            return new AuthResponseDTO
            {
                AccountId = account.Id,
                UserName = account.UserName,
                Email = account.Email,
                Role = account.Role?.ToString() ?? "User",
                AccessToken = string.Empty,
                AccessTokenExpiresAt = DateTime.UtcNow,
                RefreshToken = string.Empty,
                RefreshTokenExpiresAt = DateTime.UtcNow
            };
        }

        public async Task<AuthResponseDTO> Login(LoginDTO request)
        {
            var account = await _unitOfWork.AccountRepository.GetByEmailAsync(request.Email);
            if (account == null || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
                throw new ApiExceptionResponse("Invalid email or password.", 401);

            if (account.Status == AccountStatus.PendingVerification)
                throw new ApiExceptionResponse(
                    "Tài khoản chưa được xác thực email. Vui lòng kiểm tra hộp thư và nhập mã OTP.", 403);

            if (account.Status == AccountStatus.Suspended || account.Status == AccountStatus.Inactive)
                throw new ApiExceptionResponse("Account is not active.", 403);

            var memberships = await LoadGroupMembershipsAsync(account.Id);
            var response = await IssueTokensAsync(account, memberships);
            await _unitOfWork.CommitAsync();
            return response;
        }

        public async Task<AuthResponseDTO> GoogleLogin(GoogleLoginDTO request)
        {
            var clientId = _configuration["GoogleClientId"]
                ?? throw new ApiExceptionResponse("Google Client ID chưa được cấu hình.", 500);

            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch
            {
                throw new ApiExceptionResponse("Google token không hợp lệ hoặc đã hết hạn.", 401);
            }

            var email = payload.Email
                ?? throw new ApiExceptionResponse("Không lấy được email từ tài khoản Google.", 400);

            var account = await _unitOfWork.AccountRepository.GetByEmailAsync(email);

            if (account == null)
            {
                // Tự động tạo tài khoản mới từ thông tin Google
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    UserName = payload.Name ?? email.Split('@')[0],
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    Role = AccountRole.User,
                    Status = AccountStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _unitOfWork.AccountRepository.CreateAsync(account);
            }
            else
            {
                if (account.Status == AccountStatus.Suspended || account.Status == AccountStatus.Inactive)
                    throw new ApiExceptionResponse("Tài khoản không còn hoạt động.", 403);
            }

            var memberships = await LoadGroupMembershipsAsync(account.Id);
            var response = await IssueTokensAsync(account, memberships);
            await _unitOfWork.CommitAsync();
            return response;
        }

        public async Task<AuthResponseDTO> Refresh(RefreshTokenRequestDTO request)
        {
            var stored = await _unitOfWork.RefreshTokenRepository.GetByTokenAsync(request.RefreshToken)
                ?? throw new ApiExceptionResponse("Invalid refresh token.", 401);

            if (!stored.IsActive)
                throw new ApiExceptionResponse("Refresh token expired or revoked.", 401);

            var account = await _unitOfWork.AccountRepository.GetByIdAsync(stored.AccountId)
                ?? throw new ApiExceptionResponse("Account not found.", 401);

            // Refresh -> reload memberships để claim luôn cập nhật nhóm mới nhất
            var memberships = await LoadGroupMembershipsAsync(account.Id);
            var response = await IssueTokensAsync(account, memberships);
            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByToken = response.RefreshToken;

            await _unitOfWork.CommitAsync();
            return response;
        }

        public async Task ForgotPassword(ForgotPasswordDTO request)
        {
            var account = await _unitOfWork.AccountRepository.GetByEmailAsync(request.Email);
            // Trả về yên lặng để tránh lộ thông tin email có tồn tại hay không (email enumeration)
            if (account == null) return;

            var token = Guid.NewGuid().ToString("N");
            account.ResetPasswordToken = token;
            account.ResetPasswordTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            var frontendBase = _configuration["FrontendLocalBaseUrl"]?.TrimEnd('/');
            var resetLink = $"{frontendBase}/reset-password?email={Uri.EscapeDataString(account.Email)}&token={token}";

            var subject = "Đặt lại mật khẩu";
            var body = $"Xin chào {account.UserName},\n\n" +
                       $"Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.\n" +
                       $"Nhấp vào liên kết sau để đặt lại mật khẩu (có hiệu lực trong 15 phút):\n\n" +
                       $"{resetLink}\n\n" +
                       $"Nếu bạn không yêu cầu điều này, hãy bỏ qua email này.";

            await _emailService.SendEmailAsync(account.Email, subject, body);
        }

        public async Task ResetPassword(ResetPasswordDTO request)
        {
            var account = await _unitOfWork.AccountRepository.GetByEmailAsync(request.Email)
                ?? throw new ApiExceptionResponse("Không tìm thấy tài khoản.", 400);

            if (account.ResetPasswordToken == null ||
                account.ResetPasswordToken != request.Token ||
                account.ResetPasswordTokenExpiresAt == null ||
                account.ResetPasswordTokenExpiresAt < DateTime.UtcNow)
            {
                throw new ApiExceptionResponse("Token đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.", 400);
            }

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            account.ResetPasswordToken = null;
            account.ResetPasswordTokenExpiresAt = null;
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();
        }

        public async Task Logout(RefreshTokenRequestDTO request)
        {
            var stored = await _unitOfWork.RefreshTokenRepository.GetByTokenAsync(request.RefreshToken)
                ?? throw new ApiExceptionResponse("Invalid refresh token.", 404);

            if (stored.RevokedAt == null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task<AuthResponseDTO> VerifyOtp(VerifyOtpDTO request)
        {
            var account = await _unitOfWork.AccountRepository.GetByEmailAsync(request.Email)
                ?? throw new ApiExceptionResponse("Không tìm thấy tài khoản.", 404);

            if (account.Status != AccountStatus.PendingVerification)
                throw new ApiExceptionResponse("Tài khoản đã được xác thực hoặc không ở trạng thái chờ xác thực.", 400);

            if (account.EmailOtp == null ||
                account.EmailOtp != request.Otp ||
                account.EmailOtpExpiresAt == null ||
                account.EmailOtpExpiresAt < DateTime.UtcNow)
            {
                throw new ApiExceptionResponse("Mã OTP không hợp lệ hoặc đã hết hạn.", 400);
            }

            // Kích hoạt tài khoản
            account.Status = AccountStatus.Active;
            account.IsEmailVerified = true;
            account.EmailOtp = null;
            account.EmailOtpExpiresAt = null;
            account.UpdatedAt = DateTime.UtcNow;

            // Cấp token cho user (đăng nhập tự động sau xác thực thành công)
            var memberships = await LoadGroupMembershipsAsync(account.Id);
            var response = await IssueTokensAsync(account, memberships);
            await _unitOfWork.CommitAsync();
            return response;
        }

        public async Task ResendOtp(ResendOtpDTO request)
        {
            var account = await _unitOfWork.AccountRepository.GetByEmailAsync(request.Email)
                ?? throw new ApiExceptionResponse("Không tìm thấy tài khoản.", 404);

            if (account.Status != AccountStatus.PendingVerification)
                throw new ApiExceptionResponse("Tài khoản đã được xác thực.", 400);

            // Rate limit: không cho gửi lại nếu OTP hiện tại còn hơn 2 phút (mới tạo chưa quá 1 phút)
            if (account.EmailOtpExpiresAt != null &&
                account.EmailOtpExpiresAt > DateTime.UtcNow.AddMinutes(2))
            {
                throw new ApiExceptionResponse("Vui lòng đợi ít nhất 1 phút trước khi gửi lại mã OTP.", 429);
            }

            var otp = GenerateOtp();
            account.EmailOtp = otp;
            account.EmailOtpExpiresAt = DateTime.UtcNow.AddMinutes(3);
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();

            await _emailService.SendEmailAsync(
                account.Email,
                "Gửi lại mã xác thực - BIM CDE Portal",
                $"Xin chào {account.UserName},\n\n" +
                $"Mã xác thực (OTP) mới của bạn là: {otp}\n\n" +
                $"Mã này có hiệu lực trong 3 phút.\n" +
                $"Nếu bạn không yêu cầu, hãy bỏ qua email này.");
        }

        // Sinh access (kèm Group claims) + refresh token, lưu refresh token vào DB.
        private async Task<AuthResponseDTO> IssueTokensAsync(Account account, IEnumerable<GroupMember> memberships)
        {
            var accessToken = _jwtService.GenerateAccessToken(account, memberships);
            var refreshToken = _jwtService.GenerateRefreshToken();
            var now = DateTime.UtcNow;

            await _unitOfWork.RefreshTokenRepository.CreateAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Token = refreshToken,
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwtService.RefreshTokenDays)
            });

            return new AuthResponseDTO
            {
                AccountId = account.Id,
                UserName = account.UserName,
                Email = account.Email,
                Role = account.Role?.ToString() ?? "User",
                AccessToken = accessToken,
                AccessTokenExpiresAt = now.AddMinutes(_jwtService.AccessTokenMinutes),
                RefreshToken = refreshToken,
                RefreshTokenExpiresAt = now.AddDays(_jwtService.RefreshTokenDays)
            };
        }

        private async Task<List<GroupMember>> LoadGroupMembershipsAsync(Guid accountId)
        {
            var all = await _unitOfWork.Repository<GroupMember>()
                .FindAsync(gm => gm.AccountId == accountId);
            return all.ToList();
        }

        /// <summary>
        /// Sinh mã OTP gồm 6 chữ số ngẫu nhiên (cryptographically secure).
        /// </summary>
        private static string GenerateOtp()
        {
            var bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            var number = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
            return number.ToString("D6");
        }
    }
}
