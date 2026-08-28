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
    public class FilePermissionService : IFilePermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        private readonly IAuditLogService _auditLog;
        private readonly IPermissionCleanupService _cleanup;

        public FilePermissionService(
            IUnitOfWork unitOfWork, IMapper mapper, IAuditLogService auditLog,
            IPermissionCleanupService cleanup)
        {
            _auditLog = auditLog;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _cleanup = cleanup;
        }

        #region CRUD có sẵn
        //bo
        #endregion

        #region Lấy data cho frontend

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetPartipatedGroupFilePermissionsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items);
        }

        public async Task<FilePermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid fileItemId, Guid callerAccountId)
        {
            // Groups the caller belongs to are excluded so they cannot kick themselves out of the group.
            var callerParticipantIds = await _unitOfWork.FilePermissionRepository.GetCallerParticipantIdsByFileItemIdAsync(fileItemId, callerAccountId);

            var items = await _unitOfWork.FilePermissionRepository.GetActivePartipantsByFileItemIdAsync(fileItemId);

            var activeGroupOfFile = _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items.Values.ToList());

            var allProjectParticipants = await _unitOfWork.FilePermissionRepository.GetAllParticipantsByFileItemIdAsync(fileItemId);

            var availableGroups = allProjectParticipants
                .Where(pp => !items.ContainsKey(pp.ProjectParticipantId) && !callerParticipantIds.Contains(pp.ProjectParticipantId))
                .ToList();

            return new FilePermissionsViewModelDTO
            {
                AvailableGroups = availableGroups,
                SelectedPermissions = activeGroupOfFile
                    .Where(p => !callerParticipantIds.Contains(p.ProjectParticipantId))
                    .ToList()
            };
        }

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> GetActiveParticipantsByFileItemId(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetActivePartipantsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items.Values.ToList());
        }

        public async Task<MemberPermissionsViewModelDTO> GetMemberPermissionsAsync(Guid fileItemId, Guid callerAccountId)
        {
            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            // Resolve granting groups per the eval fallback: a group's file override wins (incl. deny);
            // groups without a file override inherit the owning folder's grant.
            var fileGrants = await _unitOfWork.FilePermissionRepository.GetActiveGroupGrantsByFileItemIdAsync(fileItemId);
            var folderGrants = await _unitOfWork.FolderPermissionRepository.GetActiveGroupGrantsByFolderIdAsync(file.FolderId);

            var fileByParticipant = fileGrants.ToDictionary(g => g.ParticipantId);

            var effective = new Dictionary<Guid, GroupGrantDTO>();
            foreach (var g in fileGrants) effective[g.ParticipantId] = g;                 // present-wins
            foreach (var g in folderGrants)
                if (!fileByParticipant.ContainsKey(g.ParticipantId))
                    effective[g.ParticipantId] = g;                                        // inherit folder

            var viewGrantByParticipant = effective
                .Where(kv => kv.Value.CanView)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var members = await _unitOfWork.FilePermissionRepository
                .GetActiveMembersByParticipantIdsAsync(viewGrantByParticipant.Keys.ToList());
            var overrides = await _unitOfWork.FilePermissionRepository.GetActiveAccountOverridesByFileItemIdAsync(fileItemId);
            var overrideByAccount = overrides.ToDictionary(kv => kv.Key, kv => (IPermissionAcl)kv.Value);

            return new MemberPermissionsViewModelDTO
            {
                Members = PermissionRosterBuilder.Build(viewGrantByParticipant, members, overrideByAccount, callerAccountId)
            };
        }

        #endregion

        #region Create/Update permissions theo bulk

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> BulkUpdateFilePermissionsAsync(AddPermissionsBulkDTO dto, Guid actorId)
        {
            //if (!dto.GroupsPermission.Any()) 
            //    throw new ApiExceptionResponse("GroupsPermission list is empty.", 400);

            var participantIds = dto.GroupsPermission.Select(u => u.ProjectParticipantId).Union(dto.RemoveParticipantIds).ToList();

            // Get all existing permissions in one query
            var existingPermissions = await _unitOfWork.FilePermissionRepository.GetFilePermissionsByFileItemIdAsync(dto.Id, participantIds);

            var updatedParticipantIds = new List<Guid>();

            // (bên tham gia, mức mới) để dựng câu audit log sau khi tra được tên nhóm.
            var auditChanges = new List<(Guid ParticipantId, string Level)>();

            // Remove permissions for participants in the removal list
            foreach (var participantId in dto.RemoveParticipantIds)
            {
                if (existingPermissions.TryGetValue(participantId, out var perm))
                {
                    // Gỡ khỏi danh sách override = trả file về kế thừa quyền thư mục (dòng Inactive).
                    PermissionLevelMapper.Apply(perm, PermissionLevel.Inherit, isFile: true);

                    updatedParticipantIds.Add(participantId);
                    auditChanges.Add((participantId, "bỏ quyền riêng, kế thừa theo thư mục"));

                }
            }

            // Create/Update permissions for participants in the update list
            var toCreate = new List<FilePermission>();


            foreach (var u in dto.GroupsPermission)
            {
                if (!existingPermissions.TryGetValue(u.ProjectParticipantId, out var permission))
                {
                    permission = new FilePermission
                    {
                        Id = Guid.NewGuid(),
                        FileItemId = dto.Id,
                        ProjectParticipantId = u.ProjectParticipantId
                    };

                    toCreate.Add(permission);
                }

                // Ánh xạ mức quyền qua nguồn chân lý duy nhất (đồng nhất với ma trận phân quyền).
                PermissionLevelMapper.Apply(
                    permission, PermissionLevelMapper.FromFlags(u.CanView, u.CanEdit), isFile: true);

                updatedParticipantIds.Add(u.ProjectParticipantId);
                auditChanges.Add((u.ProjectParticipantId, PermissionAuditDescriber.LevelName(u.CanView, u.CanEdit)));

            }

            if (toCreate.Any())
                await _unitOfWork.Repository<FilePermission>().CreateRangeAsync(toCreate);

            var auditFile = await _unitOfWork.Repository<FileItem>().GetByIdAsync(dto.Id);
            var auditFolder = auditFile == null
                ? null
                : await _unitOfWork.Repository<Folder>().GetByIdAsync(auditFile.FolderId);

            // Ghi rõ TỪNG bên và mức quyền mới thay vì chỉ đếm số bên.
            var groupNames = await PermissionAuditDescriber.ResolveGroupNamesAsync(
                _unitOfWork, auditChanges.Select(c => c.ParticipantId).ToList());
            var auditEntries = auditChanges
                .Select(c => PermissionAuditDescriber.Entry(
                    PermissionAuditDescriber.GroupNameOf(groupNames, c.ParticipantId), c.Level))
                .ToList();

            await _auditLog.LogAsync(
                Domain.Enum.Audit.LogScope.Project, Domain.Enum.Audit.AuditAction.PermissionChange,
                nameof(FileItem), dto.Id.ToString(), actorId,
                detail: $"Phân quyền nhóm trên tệp '{auditFile?.Name}': {PermissionAuditDescriber.Join(auditEntries)}",
                projectId: auditFolder?.ProjectId, folderId: auditFile?.FolderId);

            await _unitOfWork.CommitAsync();

            // [T1] Pool nhóm của file vừa đổi -> dọn override tài khoản mồ côi (chạy SAU commit để
            // recompute thấy trạng thái mới; dòng mồ côi vốn trơ dưới mask nên lệch pha vô hại).
            await _cleanup.CleanupFileOverridesAsync(dto.Id);

            var permissions = await _unitOfWork.FilePermissionRepository.GetFilePermissionsByParticipantIdsAsync(dto.Id, updatedParticipantIds);

            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(permissions);
        }

        public async Task<IEnumerable<UserPermissionResponseDTO>> BulkUpdateFileUserPermissionsAsync(AddUserPermissionsBulkDTO dto, Guid actorId)
        {
            var users = dto.UsersPermission ?? new List<AddUserPermissionDTO>();
            var removeIds = dto.RemoveAccountIds ?? new List<Guid>();

            if (users.Count == 0 && removeIds.Count == 0)
                throw new ApiExceptionResponse("No changes provided.", 400);

            // A leader cannot override their own access (mirrors the group UI hiding the caller's group).
            if (users.Any(u => u.AccountId == actorId) || removeIds.Contains(actorId))
                throw new ApiExceptionResponse("You cannot change your own permission.", 403);

            var accountIds = users.Select(u => u.AccountId).Union(removeIds).ToList();

            var existing = await _unitOfWork.FilePermissionRepository.GetAccountOverridesByFileItemIdAsync(dto.Id, accountIds);

            var updatedAccountIds = new List<Guid>();

            // (tài khoản, mức mới) để dựng câu audit log sau khi tra được tên người + nhóm của họ.
            var auditChanges = new List<(Guid AccountId, string Level)>();

            // Remove = trả tài khoản về kế thừa quyền nhóm (dòng Inactive; đường đọc lọc Active nên bỏ qua).
            foreach (var accountId in removeIds)
            {
                if (existing.TryGetValue(accountId, out var perm))
                {
                    PermissionLevelMapper.Apply(perm, PermissionLevel.Inherit, isFile: true);
                    updatedAccountIds.Add(accountId);
                    auditChanges.Add((accountId, "bỏ quyền riêng, trở lại theo nhóm"));
                }
            }

            var toCreate = new List<FilePermission>();
            foreach (var u in users)
            {
                if (!existing.TryGetValue(u.AccountId, out var permission))
                {
                    permission = new FilePermission
                    {
                        Id = Guid.NewGuid(),
                        FileItemId = dto.Id,
                        AccountId = u.AccountId
                    };
                    toCreate.Add(permission);
                }

                // Override tài khoản LUÔN dùng ngữ nghĩa isFile:true để mức N là dòng Active CHẶN (đè
                // quyền nhóm). Nếu lưu Inactive, đường đọc (lọc Active) sẽ bỏ sót lệnh chặn.
                PermissionLevelMapper.Apply(
                    permission, PermissionLevelMapper.FromFlags(u.CanView, u.CanEdit), isFile: true);
                updatedAccountIds.Add(u.AccountId);
                auditChanges.Add((u.AccountId, PermissionAuditDescriber.LevelName(u.CanView, u.CanEdit)));
            }

            if (toCreate.Any())
                await _unitOfWork.Repository<FilePermission>().CreateRangeAsync(toCreate);

            var auditFile = await _unitOfWork.Repository<FileItem>().GetByIdAsync(dto.Id);
            var auditFolder = auditFile == null
                ? null
                : await _unitOfWork.Repository<Folder>().GetByIdAsync(auditFile.FolderId);

            // Ghi rõ TÊN người và nhóm của họ thay vì chỉ đếm số người.
            var accountLabels = await PermissionAuditDescriber.ResolveAccountLabelsAsync(
                _unitOfWork, auditFolder?.ProjectId, auditChanges.Select(c => c.AccountId).ToList());
            var auditEntries = auditChanges
                .Select(c => PermissionAuditDescriber.Entry(
                    PermissionAuditDescriber.AccountLabelOf(accountLabels, c.AccountId), c.Level))
                .ToList();

            await _auditLog.LogAsync(
                Domain.Enum.Audit.LogScope.Project, Domain.Enum.Audit.AuditAction.PermissionChange,
                nameof(FileItem), dto.Id.ToString(), actorId,
                detail: $"Phân quyền người dùng trên tệp '{auditFile?.Name}': {PermissionAuditDescriber.Join(auditEntries)}",
                projectId: auditFolder?.ProjectId, folderId: auditFile?.FolderId);

            await _unitOfWork.CommitAsync();

            var rows = await _unitOfWork.FilePermissionRepository
                .GetAccountOverrideRowsByFileItemIdAsync(dto.Id, updatedAccountIds.Distinct().ToList());

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

        public async Task<GroupFilePermissionResponseDTO> GetFilePermissionOfParticipantByFileItemIdAndParticipantId(GetFilePermissionOfParticipantDTO dto)
        {
            var permission = await _unitOfWork.FilePermissionRepository.GetFilePermissionByFileItemIdAndParticipantIdAsync(dto.FileItemId, dto.ParticipantId);
            if (permission == null)
                throw new ApiExceptionResponse("No permission found for the specified participant and file item.", 404);
            return _mapper.Map<GroupFilePermissionResponseDTO>(permission);
        }



        #endregion
    }
}
