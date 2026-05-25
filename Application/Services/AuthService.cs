using Application.DTOs.RequestDTOs.Auth;
using Application.DTOs.ResponseDTOs.Auth;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Account;

namespace Application.Services
{
    // Register/Login theo idiom ChemXLab (BCrypt + ApiExceptionResponse) + refresh token xoay vòng.
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;

        public AuthService(IUnitOfWork unitOfWork, IJwtService jwtService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
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
            // Account vừa tạo chưa có Employee row -> employments rỗng
            var response = await IssueTokensAsync(account, Array.Empty<Employee>());
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

            var employments = await LoadEmploymentsAsync(account.Id);
            var response = await IssueTokensAsync(account, employments);
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

            // Refresh -> reload employments để claim luôn cập nhật role mới nhất
            var employments = await LoadEmploymentsAsync(account.Id);
            var response = await IssueTokensAsync(account, employments);
            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByToken = response.RefreshToken;

            await _unitOfWork.CommitAsync();
            return response;
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

        // Sinh access (kèm DepartmentRole claims) + refresh token, lưu refresh token vào DB.
        private async Task<AuthResponseDTO> IssueTokensAsync(Account account, IEnumerable<Employee> employments)
        {
            var accessToken = _jwtService.GenerateAccessToken(account, employments);
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

        private async Task<List<Employee>> LoadEmploymentsAsync(Guid accountId)
        {
            var all = await _unitOfWork.Repository<Employee>().GetAllAsync();
            return all.Where(e => e.AccountId == accountId).ToList();
        }
    }
}
