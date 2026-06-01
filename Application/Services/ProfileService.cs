using Application.DTOs.RequestDTOs.Profile;
using Application.DTOs.ResponseDTOs.Profile;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;

namespace Application.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public ProfileService(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<ProfileResponseDTO> GetMyProfileAsync()
        {
            var accountId = RequireAccountId();
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(accountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);

            return await BuildAsync(account);
        }

        public async Task<ProfileResponseDTO> UpdateMyProfileAsync(UpdateProfileDTO dto)
        {
            var accountId = RequireAccountId();
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(accountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);

            // Email đổi -> check duplicate
            if (!string.IsNullOrWhiteSpace(dto.Email)
                && !string.Equals(dto.Email, account.Email, StringComparison.OrdinalIgnoreCase))
            {
                if (await _unitOfWork.AccountRepository.EmailExistsAsync(dto.Email))
                    throw new ApiExceptionResponse("Email already in use.", 409);
                account.Email = dto.Email;
            }

            if (!string.IsNullOrWhiteSpace(dto.UserName))
                account.UserName = dto.UserName;

            account.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AccountRepository.Update(account);
            await _unitOfWork.CommitAsync();

            return await BuildAsync(account);
        }

        public async Task ChangePasswordAsync(ChangePasswordDTO dto)
        {
            var accountId = RequireAccountId();
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(accountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, account.PasswordHash))
                throw new ApiExceptionResponse("Current password is incorrect.", 400);

            if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, account.PasswordHash))
                throw new ApiExceptionResponse("New password must differ from current password.", 400);

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            account.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AccountRepository.Update(account);
            await _unitOfWork.CommitAsync();

            // Note: không revoke refresh token ở đây để không log-out các session khác.
            // Nếu yêu cầu "đổi pass = đăng xuất mọi nơi", thêm RevokeAllForAccount sau.
        }

        private Guid RequireAccountId()
            => _currentUser.AccountId
               ?? throw new ApiExceptionResponse("Authentication required.", 401);

        // Build profile + join group memberships để FE 1 call là đủ thông tin user.
        private async Task<ProfileResponseDTO> BuildAsync(Account account)
        {
            var memberships = (await _unitOfWork.Repository<GroupMember>().GetAllAsync())
                .Where(gm => gm.AccountId == account.Id)
                .ToList();

            IDictionary<Guid, Group> groupIndex;
            if (memberships.Count == 0)
            {
                groupIndex = new Dictionary<Guid, Group>();
            }
            else
            {
                var ids = memberships.Select(m => m.GroupId).ToHashSet();
                groupIndex = (await _unitOfWork.Repository<Group>().GetAllAsync())
                    .Where(g => ids.Contains(g.Id))
                    .ToDictionary(g => g.Id);
            }

            return new ProfileResponseDTO
            {
                Id = account.Id,
                UserName = account.UserName,
                Email = account.Email,
                Role = account.Role?.ToString(),
                Status = account.Status?.ToString(),
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                Groups = memberships.Select(m => new ProfileGroupDTO
                {
                    GroupId = m.GroupId,
                    GroupName = groupIndex.TryGetValue(m.GroupId, out var g) ? g.Name : "",
                    Role = m.Role.ToString(),
                    JoinedAt = m.JoinedAt
                }).ToList()
            };
        }
    }
}
