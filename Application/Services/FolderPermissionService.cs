using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Permission;

namespace Application.Services
{
    public class FolderPermissionService : IFolderPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        private readonly IAuditLogService _auditLog;

        public FolderPermissionService(IUnitOfWork unitOfWork, IMapper mapper, IAuditLogService auditLog)
        {
            _auditLog = auditLog;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        #region Lấy data cho frontend
        public async Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetGroupFolderPermissionResponsesAsync(Guid folderId)
        {
            var items = await _unitOfWork.FolderPermissionRepository.GetPartipatedGroupFolderPermissionsByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(items);
        }

        public async Task<FolderPermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid folderId, Guid callerAccountId)
        {
            // Groups the caller belongs to are excluded so they cannot kick themselves out of the group.
            var callerParticipantIds = await _unitOfWork.FolderPermissionRepository.GetCallerParticipantIdsByFolderIdAsync(folderId, callerAccountId);

            var items = await _unitOfWork.FolderPermissionRepository.GetActivePartipantsByFolderIdAsync(folderId);

            var activeGroupOfFolder = _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(items.Values.ToList());

            var allProjectParticipants = await _unitOfWork.FolderPermissionRepository.GetAllParticipantsByFolderIdAsync(folderId);

            var availableGroups = allProjectParticipants
                .Where(pp => !items.ContainsKey(pp.ProjectParticipantId) && !callerParticipantIds.Contains(pp.ProjectParticipantId))
                .ToList();

            return new FolderPermissionsViewModelDTO
            {
                AvailableGroups = availableGroups,
                SelectedPermissions = activeGroupOfFolder
                    .Where(p => !callerParticipantIds.Contains(p.ProjectParticipantId))
                    .ToList()
            };
        }

        public async Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetActiveParticipantsByFolderId(Guid folderId)
        {
            var items = await _unitOfWork.FolderPermissionRepository.GetActivePartipantsByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(items.Values.ToList());
        }

        public async Task<MemberPermissionsViewModelDTO> GetMemberPermissionsAsync(Guid folderId, Guid callerAccountId)
        {
            var grants = await _unitOfWork.FolderPermissionRepository.GetActiveGroupGrantsByFolderIdAsync(folderId);
            var grantByParticipant = grants.ToDictionary(g => g.ParticipantId);

            var members = await _unitOfWork.FolderPermissionRepository
                .GetActiveMembersByParticipantIdsAsync(grantByParticipant.Keys.ToList());
            var overrides = await _unitOfWork.FolderPermissionRepository.GetActiveAccountOverridesByFolderIdAsync(folderId);
            var blacklistedIds = overrides.Where(kv => !kv.Value.CanView).Select(kv => kv.Key).ToHashSet();

            return new MemberPermissionsViewModelDTO
            {
                Members = PermissionRosterBuilder.Build(grantByParticipant, members, blacklistedIds, callerAccountId)
            };
        }

        #endregion

        #region Create/Update permissions theo bulk

        public async Task<IEnumerable<GroupFolderPermissionResponseDTO>> BulkUpdateFolderPermissionsAsync(AddPermissionsBulkDTO dto, Guid actorId)
        {
            //if (!dto.GroupsPermission.Any()) 
            //    throw new ApiExceptionResponse("GroupsPermission list is empty.", 400);

            var participantIds = dto.GroupsPermission.Select(u => u.ProjectParticipantId).Union(dto.RemoveParticipantIds).ToList();

            // Get all existing permissions in one query
            var existingPermissions = await _unitOfWork.FolderPermissionRepository.GetFolderPermissionsByFolderIdAsync(dto.Id, participantIds);

            var updatedParticipantIds = new List<Guid>();

            // Remove permissions for participants in the removal list
            foreach (var participantId in dto.RemoveParticipantIds)
            {
                if (existingPermissions.TryGetValue(participantId, out var perm))
                {
                    // Gỡ khỏi danh sách = trả về trạng thái không quyền (dòng Inactive).
                    PermissionLevelMapper.Apply(perm, PermissionLevel.Inherit, isFile: false);

                    updatedParticipantIds.Add(participantId);

                }
            }

            // Create/Update permissions for participants in the update list
            var toCreate = new List<FolderPermission>();


            foreach (var u in dto.GroupsPermission)
            {
                if (!existingPermissions.TryGetValue(u.ProjectParticipantId, out var permission))
                {
                    permission = new FolderPermission
                    {
                        Id = Guid.NewGuid(),
                        FolderId = dto.Id,
                        ProjectParticipantId = u.ProjectParticipantId
                    };

                    toCreate.Add(permission);
                }

                // Ánh xạ mức quyền qua nguồn chân lý duy nhất (đồng nhất với ma trận phân quyền).
                PermissionLevelMapper.Apply(
                    permission, PermissionLevelMapper.FromFlags(u.CanView, u.CanEdit), isFile: false);

                updatedParticipantIds.Add(u.ProjectParticipantId);

            }

            if (toCreate.Any())
                await _unitOfWork.Repository<FolderPermission>().CreateRangeAsync(toCreate);

            var auditFolder = await _unitOfWork.Repository<Folder>().GetByIdAsync(dto.Id);
            await _auditLog.LogAsync(
                Domain.Enum.Audit.LogScope.Project, Domain.Enum.Audit.AuditAction.PermissionChange,
                nameof(Folder), dto.Id.ToString(), actorId,
                detail: $"Cập nhật phân quyền thư mục '{auditFolder?.Name}' cho {updatedParticipantIds.Count()} bên tham gia",
                projectId: auditFolder?.ProjectId, folderId: dto.Id);

            await _unitOfWork.CommitAsync();

            var permissions = await _unitOfWork.FolderPermissionRepository.GetFolderPermissionsByParticipantIdsAsync(dto.Id, updatedParticipantIds);

            return _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(permissions);
        }

        public async Task<IEnumerable<UserPermissionResponseDTO>> BulkUpdateFolderUserPermissionsAsync(AddUserPermissionsBulkDTO dto, Guid actorId)
        {
            var users = dto.UsersPermission ?? new List<AddUserPermissionDTO>();
            var removeIds = dto.RemoveAccountIds ?? new List<Guid>();

            if (users.Count == 0 && removeIds.Count == 0)
                throw new ApiExceptionResponse("No changes provided.", 400);

            // A leader cannot override their own access (mirrors the group UI hiding the caller's group).
            if (users.Any(u => u.AccountId == actorId) || removeIds.Contains(actorId))
                throw new ApiExceptionResponse("You cannot change your own permission.", 403);

            var accountIds = users.Select(u => u.AccountId).Union(removeIds).ToList();

            var existing = await _unitOfWork.FolderPermissionRepository.GetAccountOverridesByFolderIdAsync(dto.Id, accountIds);

            var updatedAccountIds = new List<Guid>();

            // Remove = trả tài khoản về kế thừa quyền nhóm (dòng Inactive; đường đọc lọc Active nên bỏ qua).
            foreach (var accountId in removeIds)
            {
                if (existing.TryGetValue(accountId, out var perm))
                {
                    PermissionLevelMapper.Apply(perm, PermissionLevel.Inherit, isFile: true);
                    updatedAccountIds.Add(accountId);
                }
            }

            var toCreate = new List<FolderPermission>();
            foreach (var u in users)
            {
                if (!existing.TryGetValue(u.AccountId, out var permission))
                {
                    permission = new FolderPermission
                    {
                        Id = Guid.NewGuid(),
                        FolderId = dto.Id,
                        AccountId = u.AccountId
                    };
                    toCreate.Add(permission);
                }

                // Override tài khoản LUÔN dùng ngữ nghĩa isFile:true để mức N là dòng Active CHẶN (đè
                // quyền nhóm), áp cho cả cây con. Nếu lưu Inactive, đường đọc (lọc Active) sẽ bỏ sót chặn.
                PermissionLevelMapper.Apply(
                    permission, PermissionLevelMapper.FromFlags(u.CanView, u.CanEdit), isFile: true);
                updatedAccountIds.Add(u.AccountId);
            }

            if (toCreate.Any())
                await _unitOfWork.Repository<FolderPermission>().CreateRangeAsync(toCreate);

            var auditFolder = await _unitOfWork.Repository<Folder>().GetByIdAsync(dto.Id);
            await _auditLog.LogAsync(
                Domain.Enum.Audit.LogScope.Project, Domain.Enum.Audit.AuditAction.PermissionChange,
                nameof(Folder), dto.Id.ToString(), actorId,
                detail: $"Cập nhật phân quyền người dùng cho thư mục '{auditFolder?.Name}': {updatedAccountIds.Distinct().Count()} người",
                projectId: auditFolder?.ProjectId, folderId: dto.Id);

            await _unitOfWork.CommitAsync();

            var rows = await _unitOfWork.FolderPermissionRepository
                .GetAccountOverrideRowsByFolderIdAsync(dto.Id, updatedAccountIds.Distinct().ToList());

            return rows.Select(fp => new UserPermissionResponseDTO
            {
                Id = fp.Id,
                AccountId = fp.AccountId!.Value,
                UserName = fp.Account?.UserName ?? string.Empty,
                Email = fp.Account?.Email ?? string.Empty,
                CanView = fp.CanView,
                CanEdit = fp.CanEdit,
                Status = fp.Status
            }).ToList();
        }

        #endregion


        #region Check quyền

        public async Task<GroupFolderPermissionResponseDTO> GetFolderPermissionOfParticipantByFolderIdAndParticipantId(GetFolderPermissionOfParticipantDTO dto)
        {
            var permission = await _unitOfWork.FolderPermissionRepository.GetFolderPermissionByFolderIdAndParticipantIdAsync(dto.FolderId, dto.ParticipantId);
            if (permission == null)
                throw new ApiExceptionResponse("No permission found for the specified participant and folder item.", 404);
            return _mapper.Map<GroupFolderPermissionResponseDTO>(permission);
        }



        #endregion
    }
}
