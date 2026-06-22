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
            if (await _unitOfWork.AccountRepository.EmailExistsAsync(request.Email))
                throw new ApiExceptionResponse("Email already exists.", 409);

            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserName = request.UserName,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = AccountRole.User,
                Status = AccountStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.AccountRepository.CreateAsync(account);
            // Account vừa tạo chưa có GroupMember -> empty
            var response = await IssueTokensAsync(account, Array.Empty<GroupMember>());
            await _unitOfWork.CommitAsync();
            return response;
        }

        public async Task<AuthResponseDTO> Login(LoginDTO request)
        {
            var account = await _unitOfWork.AccountRepository.GetByEmailAsync(request.Email);
            if (account == null || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
                throw new ApiExceptionResponse("Invalid email or password.", 401);

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
    }
}
