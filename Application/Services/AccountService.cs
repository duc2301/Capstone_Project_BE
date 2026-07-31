using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.ResponseDTOs.Account;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Audit;

namespace Application.Services
{
    public class AccountService : IAccountService
    {
        private const string AvatarPrefix = "avatars";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLog;
        private readonly IImageUploadService _imageUpload;

        public AccountService(
            IUnitOfWork unitOfWork, IMapper mapper, IAuditLogService auditLog, IImageUploadService imageUpload)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _auditLog = auditLog;
            _imageUpload = imageUpload;
        }

        public async Task<IEnumerable<AccountResponseDTO>> GetAllAsync()
        {
            var items = (await _unitOfWork.AccountRepository.GetAllAsync(nameof(Account.Organization))).ToList();
            var dtos = _mapper.Map<List<AccountResponseDTO>>(items);

            var avatarPaths = items.Select(a => a.AvatarStoragePath).ToList();
            for (var i = 0; i < dtos.Count; i++)
                dtos[i].AvatarUrl = await _imageUpload.GetImageUrlAsync(avatarPaths[i]);

            await AttachManagedProjectsAsync(dtos);
            return dtos;
        }

        public async Task<AccountResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id);
            return entity == null ? null : await BuildResponseAsync(entity);
        }

        public async Task<AccountResponseDTO> CreateAsync(CreateAccountDTO dto, Guid actorId)
        {
            if (await _unitOfWork.AccountRepository.EmailExistsAsync(dto.Email))
                throw new ApiExceptionResponse("Email already exists.", 409);

            if (dto.OrganizationId.HasValue)
                await GetOrganizationOrThrowAsync(dto.OrganizationId.Value);

            var account = _mapper.Map<Account>(dto);
            account.Id = Guid.NewGuid();
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            account.Status = AccountStatus.Active;
            account.Role = AccountRole.User;
            account.CreatedAt = DateTime.UtcNow;
            account.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.AccountRepository.CreateAsync(account);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Create, nameof(Account), account.Id.ToString(), actorId,
                detail: $"Tạo tài khoản '{account.UserName}' ({account.Email}) — vai trò {account.Role}");

            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(account);
        }

        public async Task<AccountResponseDTO> UpdateAsync(Guid id, UpdateAccountDTO dto, Guid actorId)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            if (dto.OrganizationId.HasValue)
                await GetOrganizationOrThrowAsync(dto.OrganizationId.Value);

            _mapper.Map(dto, entity);

            if (dto.ClearOrganization == true)
                entity.OrganizationId = null;

            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AccountRepository.Update(entity);

            var revokedSessions = 0;
            if (entity.Status != AccountStatus.Active)
                revokedSessions = await RevokeActiveRefreshTokensAsync(entity.Id);

            var sessionNote = revokedSessions > 0 ? $" — thu hồi {revokedSessions} phiên đăng nhập" : string.Empty;
            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Update, nameof(Account), entity.Id.ToString(), actorId,
                detail: $"Cập nhật tài khoản '{entity.UserName}' — vai trò {entity.Role}, trạng thái {entity.Status}{sessionNote}");

            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(entity);
        }

        public async Task DeleteAsync(Guid id, Guid actorId)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            _unitOfWork.AccountRepository.Delete(entity);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Delete, nameof(Account), entity.Id.ToString(), actorId,
                detail: $"Xoá tài khoản '{entity.UserName}' ({entity.Email})");

            await _unitOfWork.CommitAsync();
        }

        public async Task<AccountResponseDTO> SetAvatarAsync(
            Guid id, Stream content, string fileName, long sizeBytes, Guid actorId, CancellationToken ct = default)
        {
            var entity = await _unitOfWork.AccountRepository.GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Account with ID {id} not found.", 404);

            entity.AvatarStoragePath = await _imageUpload.SaveImageAsync(
                content, fileName, sizeBytes, $"{AvatarPrefix}/{id}", ct);
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.AccountRepository.Update(entity);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Update, nameof(Account), entity.Id.ToString(), actorId,
                detail: $"Cập nhật ảnh đại diện tài khoản '{entity.UserName}'");

            await _unitOfWork.CommitAsync();

            return await BuildResponseAsync(entity);
        }

        private async Task<int> RevokeActiveRefreshTokensAsync(Guid accountId)
        {
            var tokens = (await _unitOfWork.RefreshTokenRepository
                    .FindAsync(rt => rt.AccountId == accountId && rt.RevokedAt == null))
                .ToList();

            foreach (var token in tokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                _unitOfWork.RefreshTokenRepository.Update(token);
            }

            return tokens.Count;
        }

        private async Task<Organization> GetOrganizationOrThrowAsync(Guid organizationId)
        {
            return await _unitOfWork.Repository<Organization>().GetByIdAsync(organizationId)
                ?? throw new ApiExceptionResponse($"Organization with ID {organizationId} not found.", 404);
        }

        private async Task<AccountResponseDTO> BuildResponseAsync(Account entity)
        {
            var dto = _mapper.Map<AccountResponseDTO>(entity);

            if (entity.OrganizationId.HasValue)
            {
                var organization = await _unitOfWork.Repository<Organization>()
                    .GetByIdAsync(entity.OrganizationId.Value);
                dto.OrganizationName = organization == null
                    ? null
                    : (organization.DisplayName ?? organization.LegalName);
            }

            dto.AvatarUrl = await _imageUpload.GetImageUrlAsync(entity.AvatarStoragePath);

            await AttachManagedProjectsAsync(new[] { dto });
            return dto;
        }

        private async Task AttachManagedProjectsAsync(IReadOnlyCollection<AccountResponseDTO> accounts)
        {
            if (accounts.Count == 0) return;

            var accountIds = accounts.Select(a => a.Id).ToList();
            var projects = await _unitOfWork.Repository<Project>()
                .FindAsync(p => p.ManagerAccountId != null && accountIds.Contains(p.ManagerAccountId.Value));

            var byManager = projects
                .GroupBy(p => p.ManagerAccountId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => new AccountManagedProjectDTO
                    {
                        ProjectId = p.Id,
                        ProjectName = p.ProjectName
                    }).ToList());

            foreach (var account in accounts)
            {
                if (byManager.TryGetValue(account.Id, out var managed))
                    account.ManagedProjects = managed;
            }
        }
    }
}
