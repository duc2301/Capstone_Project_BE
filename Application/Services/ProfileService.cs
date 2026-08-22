using Application.DTOs.RequestDTOs.Profile;
using Application.DTOs.ResponseDTOs.Profile;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Group;
using Domain.Enum.Project;

namespace Application.Services
{
    public class ProfileService : IProfileService
    {
        private const string AvatarPrefix = "avatars";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IImageUploadService _imageUpload;
        private readonly IAuditLogService _auditLog;

        public ProfileService(
            IUnitOfWork unitOfWork, IImageUploadService imageUpload, IAuditLogService auditLog)
        {
            _unitOfWork = unitOfWork;
            _imageUpload = imageUpload;
            _auditLog = auditLog;
        }

        public async Task<ProfileResponseDTO> GetMyProfileAsync(Guid accountId)
        {
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(accountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);

            return await BuildAsync(account);
        }

        public async Task<ProfileResponseDTO> UpdateMyProfileAsync(Guid accountId, UpdateProfileDTO dto)
        {
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

        public async Task ChangePasswordAsync(Guid accountId, ChangePasswordDTO dto)
        {
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

        public async Task<ProfileResponseDTO> SetMyAvatarAsync(
            Guid accountId, Stream content, string fileName, long sizeBytes, CancellationToken ct = default)
        {
            var account = await _unitOfWork.AccountRepository.GetByIdAsync(accountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);

            var previousAvatarPath = account.AvatarStoragePath;

            account.AvatarStoragePath = await _imageUpload.SaveImageAsync(
                content, fileName, sizeBytes, $"{AvatarPrefix}/{accountId}", ct);
            account.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AccountRepository.Update(account);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Update, nameof(Account), account.Id.ToString(), accountId,
                detail: "Cập nhật ảnh đại diện cá nhân");

            await _unitOfWork.CommitAsync();

            // Sau commit: bản ghi đã trỏ sang ảnh mới nên ảnh cũ không còn ai tham chiếu.
            await _imageUpload.DeleteImageAsync(previousAvatarPath, ct);

            return await BuildAsync(account);
        }

        // Build profile + join group memberships để FE 1 call là đủ thông tin user.
        private async Task<ProfileResponseDTO> BuildAsync(Account account)
        {
            var memberships = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.AccountId == account.Id && gm.Status == GroupMemberStatus.Active))
                .ToList();

            var groupIds = memberships.Select(m => m.GroupId).ToHashSet();
            var groupIndex = await LoadGroupsAsync(groupIds);
            var projectsByGroup = await LoadGroupProjectsAsync(groupIds);
            var organizationNames = await LoadOrganizationNamesAsync(
                CollectOrganizationIds(account, groupIndex.Values));

            return new ProfileResponseDTO
            {
                Id = account.Id,
                UserName = account.UserName,
                Email = account.Email,
                Role = account.Role?.ToString(),
                Status = account.Status?.ToString(),
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                AvatarUrl = await _imageUpload.GetImageUrlAsync(account.AvatarStoragePath),
                IsEmailVerified = account.IsEmailVerified,
                OrganizationId = account.OrganizationId,
                OrganizationName = LookupOrganizationName(organizationNames, account.OrganizationId),
                Groups = memberships.Select(m =>
                {
                    var group = groupIndex.TryGetValue(m.GroupId, out var g) ? g : null;

                    return new ProfileGroupDTO
                    {
                        GroupId = m.GroupId,
                        GroupName = group?.Name ?? "",
                        Role = m.Role.ToString(),
                        JoinedAt = m.JoinedAt,
                        OrganizationName = LookupOrganizationName(organizationNames, group?.OrganizationId),
                        Projects = projectsByGroup.TryGetValue(m.GroupId, out var projects)
                            ? projects
                            : new List<ProfileGroupProjectDTO>()
                    };
                }).ToList()
            };
        }

        private async Task<IDictionary<Guid, Group>> LoadGroupsAsync(HashSet<Guid> groupIds)
        {
            if (groupIds.Count == 0) return new Dictionary<Guid, Group>();

            return (await _unitOfWork.Repository<Group>().FindAsync(g => groupIds.Contains(g.Id)))
                .ToDictionary(g => g.Id);
        }

        private async Task<IDictionary<Guid, List<ProfileGroupProjectDTO>>> LoadGroupProjectsAsync(
            HashSet<Guid> groupIds)
        {
            if (groupIds.Count == 0) return new Dictionary<Guid, List<ProfileGroupProjectDTO>>();

            var participants = (await _unitOfWork.Repository<ProjectParticipant>()
                    .FindAsync(pp => groupIds.Contains(pp.GroupId)
                                  && pp.Status == ProjectParticipantStatus.Active))
                .ToList();

            if (participants.Count == 0) return new Dictionary<Guid, List<ProfileGroupProjectDTO>>();

            var projectIds = participants.Select(p => p.ProjectId).ToHashSet();
            var projectNames = (await _unitOfWork.Repository<Project>()
                    .FindAsync(p => projectIds.Contains(p.Id)))
                .ToDictionary(p => p.Id, p => p.ProjectName);

            return participants
                .GroupBy(p => p.GroupId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => new ProfileGroupProjectDTO
                    {
                        ProjectId = p.ProjectId,
                        ProjectName = projectNames.TryGetValue(p.ProjectId, out var name) ? name : ""
                    }).ToList());
        }

        private async Task<IDictionary<Guid, string>> LoadOrganizationNamesAsync(HashSet<Guid> organizationIds)
        {
            if (organizationIds.Count == 0) return new Dictionary<Guid, string>();

            return (await _unitOfWork.Repository<Organization>()
                    .FindAsync(o => organizationIds.Contains(o.Id)))
                .ToDictionary(o => o.Id, o => o.DisplayName ?? o.LegalName);
        }

        private static HashSet<Guid> CollectOrganizationIds(Account account, IEnumerable<Group> groups)
        {
            var ids = groups
                .Where(g => g.OrganizationId.HasValue)
                .Select(g => g.OrganizationId!.Value)
                .ToHashSet();

            if (account.OrganizationId.HasValue) ids.Add(account.OrganizationId.Value);

            return ids;
        }

        private static string? LookupOrganizationName(IDictionary<Guid, string> names, Guid? organizationId)
        {
            if (!organizationId.HasValue) return null;
            return names.TryGetValue(organizationId.Value, out var name) ? name : null;
        }
    }
}
