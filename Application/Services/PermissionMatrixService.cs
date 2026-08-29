using Application.DTOs.RequestDTOs.PermissionMatrix;
using Application.DTOs.ResponseDTOs.Folder;
using Application.DTOs.ResponseDTOs.PermissionMatrix;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.Permission;

namespace Application.Services
{
    /// <summary>
    /// Dựng và lưu ma trận phân quyền. Đọc theo lô qua IPermissionMatrixRepository (tránh N+1),
    /// tái dùng FolderTreeService cho phần lọc/hiển thị cây, và PermissionLevelMapper cho hợp đồng
    /// lưu trữ. Đường đọc quyền vẫn nằm ở PermissionCheckingService (không đụng tới).
    /// </summary>
    public class PermissionMatrixService : IPermissionMatrixService
    {
        private readonly IPermissionMatrixRepository _matrixRepo;
        private readonly IPermissionCheckingService _permissionChecking;
        private readonly IFolderTreeService _folderTreeService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogService _auditLog;
        private readonly IPermissionCleanupService _cleanup;

        public PermissionMatrixService(
            IPermissionMatrixRepository matrixRepo,
            IPermissionCheckingService permissionChecking,
            IFolderTreeService folderTreeService,
            IUnitOfWork unitOfWork,
            IAuditLogService auditLog,
            IPermissionCleanupService cleanup)
        {
            _matrixRepo = matrixRepo;
            _permissionChecking = permissionChecking;
            _folderTreeService = folderTreeService;
            _unitOfWork = unitOfWork;
            _auditLog = auditLog;
            _cleanup = cleanup;
        }

        // ===== GET =====

        public async Task<PermissionMatrixResponseDTO> GetMatrixAsync(
            Guid projectId, Guid accountId, bool isSystemAdmin, PermissionMatrixFilterDTO? filter = null)
        {
            filter ??= new PermissionMatrixFilterDTO();
            var area = filter.Area;

            if (!await _matrixRepo.ProjectExistsAsync(projectId))
                throw new ApiExceptionResponse("Project not found.", 404);

            var isFullAccess = isSystemAdmin
                || await _permissionChecking.HasProjectFullAccessAsync(projectId, accountId);
            if (!isFullAccess && !await _matrixRepo.IsLeaderInProjectAsync(projectId, accountId))
                throw new ApiExceptionResponse("You do not have permission to access the permission matrix.", 403);

            // Chỉ Admin hệ thống + PM (KHÔNG gồm PA) được thấy & sửa quyền vùng Published/Archived trên ma trận.
            var canManageRestrictedAreas = isSystemAdmin
                || await _matrixRepo.IsProjectManagerAsync(projectId, accountId);

            // Cột = bên tham gia đang hoạt động, TRỪ nhóm của chính caller (không tự sửa quyền nhóm mình).
            var callerParticipantIds = await _matrixRepo.GetCallerParticipantIdsAsync(projectId, accountId);
            var participants = await _matrixRepo.GetActiveParticipantsByProjectAsync(projectId);
            // Lọc nhóm (multi-select theo GroupId). Rỗng = không lọc.
            var groupFilter = filter.GroupIds is { Count: > 0 }
                ? filter.GroupIds.ToHashSet()
                : null;
            var columns = participants
                .Where(pp => !callerParticipantIds.Contains(pp.Id))
                .Where(pp => groupFilter == null || groupFilter.Contains(pp.GroupId))
                .Select(pp => new MatrixColumnDTO
                {
                    ProjectParticipantId = pp.Id,
                    GroupId = pp.GroupId,
                    GroupName = pp.Group?.Name ?? string.Empty
                })
                .ToList();

            // Hàng thư mục: tái dùng cây đã lọc. Truyền isFullAccess làm cờ "không giới hạn" để
            // admin/PM/PA thấy toàn bộ cây (kể cả WIP), còn leader chỉ thấy nhánh mình được View.
            var treeRoots = await _folderTreeService.GetTreeAsync(projectId, accountId, isFullAccess, area);

            var flatFolders = new List<(FolderTreeNodeDTO node, Guid? displayParentId)>();
            void Walk(FolderTreeNodeDTO n, Guid? parentId)
            {
                // Vùng Published/Archived: Admin/PM thấy đầy đủ (sửa được); người khác chỉ thấy folder GỐC
                // của vùng (hiển thị cho biết vùng tồn tại) rồi dừng — không lộ folder con/file bên trong.
                var restrictedForCaller = !canManageRestrictedAreas && IsExcludedArea(n.Area);
                var isRoot = n.ParentFolderId == null;
                if (restrictedForCaller && !isRoot) return;

                flatFolders.Add((n, parentId));

                if (restrictedForCaller) return;   // giữ tiêu đề vùng gốc, không đệ quy vào con
                foreach (var child in n.Children) Walk(child, n.Id);
            }
            foreach (var root in treeRoots) Walk(root, null);

            var visibleFolderIds = flatFolders.Select(f => f.node.Id).ToHashSet();

            // Vùng CDE của từng folder — để chọn cổng quyền theo vùng.
            var folderAreaById = flatFolders.ToDictionary(f => f.node.Id, f => f.node.Area);

            // Folder leader quản lý được (sửa ô quyền) tùy theo VÙNG, KHÔNG kế thừa xuống theo cây:
            //   Shared -> chỉ cần quyền XEM (vùng chia sẻ, được phép điều phối quyền cho nhóm khác)
            //   WIP    -> cần quyền GHI (vùng làm việc riêng)
            // Published/Archived đã chặn ở trên (chỉ Admin/PM). Full-access sửa mọi folder (empty + cờ isFullAccess).
            var editableFolderIds = new HashSet<Guid>();
            if (!isFullAccess)
            {
                var wipFolderIds = flatFolders
                    .Where(f => f.node.ParentFolderId != null && f.node.Area == CdeArea.Wip)
                    .Select(f => f.node.Id).ToList();
                var sharedFolderIds = flatFolders
                    .Where(f => f.node.ParentFolderId != null && f.node.Area == CdeArea.Shared)
                    .Select(f => f.node.Id).ToList();

                editableFolderIds = await _permissionChecking.GetEditableFolderIdsAsync(accountId, wipFolderIds);
                editableFolderIds.UnionWith(
                    await _permissionChecking.GetViewableFolderIdsAmongAsync(accountId, sharedFolderIds));
            }

            // Hiển thị file: full-access thấy mọi file trong folder hiện; leader CHỈ thấy file ở folder
            // mình có quyền GHI (editableFolderIds). Không có quyền ghi thì không thấy file trên ma trận.
            // Với người không quản vùng Published/Archived, không lấy file của các folder thuộc vùng đó
            // (kể cả folder gốc vẫn hiển thị làm tiêu đề).
            // [KILL-A] Group file-permissioning has been retired: the matrix shows FOLDERS ONLY,
            // no file rows. Feeding no folders here means no files load, no per-file permission
            // checks run, and the file-row loop further down emits nothing. The file machinery is
            // left in place (dead) so this is fully reversible — to bring file rows back, restore
            // the two statements preserved in the comment below.
            // var fileScopeFolderIds = isFullAccess ? visibleFolderIds : editableFolderIds;
            // var fileFolderIds = flatFolders
            //     .Where(f => canManageRestrictedAreas || !IsExcludedArea(f.node.Area))
            //     .Select(f => f.node.Id)
            //     .Where(fileScopeFolderIds.Contains)
            //     .ToList();
            var fileFolderIds = new List<Guid>();

            var files = await _matrixRepo.GetFilesByFolderIdsAsync(fileFolderIds);
            var fileIds = files.Select(f => f.Id).ToList();

            // Leader: file hiện & sửa được theo VÙNG của folder chứa nó — Shared cần XEM, WIP cần GHI trên
            // CHÍNH file (quyền không kế thừa xuống; file bị override chặn cho nhóm caller cũng bị loại).
            // Full-access (null) sửa mọi file.
            HashSet<Guid>? editableFileIds = null;
            if (!isFullAccess)
            {
                var wipFileIds = files
                    .Where(f => folderAreaById.TryGetValue(f.FolderId, out var a) && a == CdeArea.Wip)
                    .Select(f => f.Id).ToList();
                var sharedFileIds = files
                    .Where(f => folderAreaById.TryGetValue(f.FolderId, out var a) && a == CdeArea.Shared)
                    .Select(f => f.Id).ToList();

                editableFileIds = await _permissionChecking.GetEditableFileIdsAsync(accountId, wipFileIds);
                editableFileIds.UnionWith(
                    await _permissionChecking.GetViewableFileIdsAmongAsync(accountId, sharedFileIds));
            }

            var folderPermIndex = IndexFolderPerms(
                await _matrixRepo.GetActiveFolderPermissionsByFolderIdsAsync(visibleFolderIds.ToList()));
            var filePermIndex = IndexFilePerms(
                await _matrixRepo.GetActiveFilePermissionsByFileIdsAsync(fileIds));

            var filesByFolder = files
                .GroupBy(f => f.FolderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Leader: chỉ giữ folder có quyền GHI + tiêu đề vùng gốc (khung cây). Folder không có quyền ghi
            // bị ẩn hẳn; folder/file được giữ sẽ neo lại vào tổ tiên còn hiển thị gần nhất để cây không đứt.
            HashSet<Guid>? keptFolderIds = null;
            Dictionary<Guid, Guid?>? displayParentById = null;
            if (!isFullAccess)
            {
                keptFolderIds = new HashSet<Guid>(editableFolderIds);
                foreach (var f in flatFolders)
                    if (f.node.ParentFolderId == null) keptFolderIds.Add(f.node.Id);
                displayParentById = flatFolders.ToDictionary(f => f.node.Id, f => f.displayParentId);
            }

            // Tổ tiên hiển thị gần nhất còn được giữ (bỏ qua folder bị ẩn). Full-access: giữ nguyên cha.
            Guid? NearestKeptFolderId(Guid? parentId)
            {
                if (keptFolderIds == null) return parentId;
                var seen = new HashSet<Guid>();
                while (parentId.HasValue && seen.Add(parentId.Value))
                {
                    if (keptFolderIds.Contains(parentId.Value)) return parentId;
                    parentId = displayParentById!.TryGetValue(parentId.Value, out var p) ? p : null;
                }
                return null;
            }

            var rows = new List<MatrixRowDTO>();
            foreach (var (node, displayParentId) in flatFolders)
            {
                var isRoot = node.ParentFolderId == null;

                // Leader: ẩn folder không có quyền GHI (giữ tiêu đề vùng gốc). File/subfolder bên trong
                // đã bị lọc riêng và sẽ neo vào tổ tiên còn hiển thị.
                if (keptFolderIds != null && !isRoot && !keptFolderIds.Contains(node.Id)) continue;

                var folderEditable = !isRoot && (isFullAccess || editableFolderIds.Contains(node.Id));

                rows.Add(new MatrixRowDTO
                {
                    TargetId = node.Id,
                    TargetType = MatrixTargetType.Folder,
                    ParentRowId = NearestKeptFolderId(displayParentId),
                    Name = node.Name,
                    Area = node.Area,
                    IsRootArea = isRoot,
                    Assignable = !isRoot,
                    Cells = columns.Select(col => new MatrixCellDTO
                    {
                        ProjectParticipantId = col.ProjectParticipantId,
                        Level = FolderCellLevel(folderPermIndex, node.Id, col.ProjectParticipantId),
                        IsInherited = false,
                        Editable = folderEditable
                    }).ToList()
                });

                if (!filesByFolder.TryGetValue(node.Id, out var folderFiles)) continue;

                foreach (var file in folderFiles)
                {
                    // Leader: ẩn HẲN file không có quyền GHI (không chỉ khoá ô) — không có quyền thì không
                    // thấy trên ma trận. editableFileIds == null nghĩa là full-access (thấy & sửa mọi file).
                    if (editableFileIds != null && !editableFileIds.Contains(file.Id)) continue;

                    var fileEditable = isFullAccess || (editableFileIds != null && editableFileIds.Contains(file.Id));
                    rows.Add(new MatrixRowDTO
                    {
                        TargetId = file.Id,
                        TargetType = MatrixTargetType.File,
                        ParentRowId = node.Id,
                        Name = file.Name,
                        Area = node.Area,
                        IsRootArea = false,
                        Assignable = true,
                        Cells = columns.Select(col =>
                        {
                            var (level, inherited) = ResolveFileCell(
                                filePermIndex, folderPermIndex, file.Id, node.Id, col.ProjectParticipantId);
                            return new MatrixCellDTO
                            {
                                ProjectParticipantId = col.ProjectParticipantId,
                                Level = level,
                                IsInherited = inherited,
                                Editable = fileEditable
                            };
                        }).ToList()
                    });
                }
            }

            // Lọc hàng theo folder/file (multi-select). Giữ hàng khớp + hậu duệ của folder khớp
            // + tổ tiên (để cây hiển thị đúng đường dẫn). Rỗng = giữ nguyên.
            if (filter.HasRowFilter)
                rows = ApplyRowFilter(rows, filter.FolderIds, filter.FileIds);

            return new PermissionMatrixResponseDTO
            {
                ProjectId = projectId,
                Columns = columns,
                Rows = rows,
                Actor = new MatrixActorScopeDTO
                {
                    IsFullAccess = isFullAccess,
                    EditableFolderIds = isFullAccess ? new List<Guid>() : editableFolderIds.ToList()
                }
            };
        }

        // ===== PUT =====

        public async Task<List<MatrixCellResultDTO>> SaveMatrixAsync(
            Guid projectId, SavePermissionMatrixDTO dto, Guid accountId, bool isSystemAdmin)
        {
            if (!await _matrixRepo.ProjectExistsAsync(projectId))
                throw new ApiExceptionResponse("Project not found.", 404);

            var isFullAccess = isSystemAdmin
                || await _permissionChecking.HasProjectFullAccessAsync(projectId, accountId);
            if (!isFullAccess && !await _matrixRepo.IsLeaderInProjectAsync(projectId, accountId))
                throw new ApiExceptionResponse("You do not have permission to edit the permission matrix.", 403);

            // Chỉ Admin hệ thống + PM (KHÔNG gồm PA) được sửa quyền vùng Published/Archived.
            var canManageRestrictedAreas = isSystemAdmin
                || await _matrixRepo.IsProjectManagerAsync(projectId, accountId);

            var changes = dto.Changes ?? new List<MatrixCellChangeDTO>();
            if (changes.Count == 0)
                throw new ApiExceptionResponse("No changes provided.", 400);

            // Dữ liệu tham chiếu để kiểm tra hợp lệ.
            var projectFolders = await _matrixRepo.GetProjectFoldersAsync(projectId);
            var folderById = projectFolders.ToDictionary(f => f.Id);
            var rootAreaIds = projectFolders.Where(f => f.ParentFolderId == null).Select(f => f.Id).ToHashSet();

            var participantIds = (await _matrixRepo.GetActiveParticipantsByProjectAsync(projectId))
                .Select(pp => pp.Id).ToHashSet();

            // Nhóm của chính caller không nằm trên ma trận -> chặn luôn ở đường ghi (tránh payload dựng tay).
            var callerParticipantIds = await _matrixRepo.GetCallerParticipantIdsAsync(projectId, accountId);

            var fileChangeIds = changes
                .Where(c => c.TargetType == MatrixTargetType.File)
                .Select(c => c.TargetId).Distinct().ToList();
            var fileById = (await _matrixRepo.GetFilesByIdsAsync(fileChangeIds)).ToDictionary(f => f.Id);

            // Kiểm tra TOÀN BỘ lô trước khi ghi.
            foreach (var c in changes)
            {
                if (!participantIds.Contains(c.ProjectParticipantId))
                    throw new ApiExceptionResponse("Participant does not belong to this project.", 400);

                if (callerParticipantIds.Contains(c.ProjectParticipantId))
                    throw new ApiExceptionResponse("You cannot change permissions for your own group.", 403);

                if (c.TargetType == MatrixTargetType.Folder)
                {
                    if (!folderById.TryGetValue(c.TargetId, out var targetFolder))
                        throw new ApiExceptionResponse("Folder does not belong to this project.", 400);
                    // Nhóm CHỦ SỞ HỮU folder chỉ bị đổi quyền bởi Admin/PM/PA (isFullAccess); leader nhóm
                    // khác được mời vào không được gỡ/hạ quyền của chủ sở hữu.
                    if (!isFullAccess && targetFolder.OwnerParticipantId == c.ProjectParticipantId)
                        throw new ApiExceptionResponse("You cannot change the owning group's permission on this folder.", 403);
                    if (IsExcludedArea(targetFolder.Area) && !canManageRestrictedAreas)
                        throw new ApiExceptionResponse("Only admin/PM can assign permissions in Published/Archived areas.", 403);
                    if (rootAreaIds.Contains(c.TargetId))
                        throw new ApiExceptionResponse("Root areas are not assignable.", 403);
                    if (c.Level == PermissionLevel.Inherit)
                        throw new ApiExceptionResponse("Folders cannot inherit; use N/R/W.", 400);
                    // Cổng quyền theo VÙNG trên chính folder (không kế thừa từ folder cha) — khớp phần hiển thị:
                    // Shared cần XEM, WIP cần GHI. Chặn leo thang qua cây.
                    if (!isFullAccess)
                    {
                        var canManageFolder = targetFolder.Area == CdeArea.Shared
                            ? await _permissionChecking.HasViewFolderAsync(c.TargetId, accountId)
                            : await _permissionChecking.HasEditFolderAsync(c.TargetId, accountId);
                        if (!canManageFolder)
                            throw new ApiExceptionResponse("You cannot assign permissions on this folder.", 403);
                    }
                }
                else
                {
                    if (!fileById.TryGetValue(c.TargetId, out var file) || !folderById.TryGetValue(file.FolderId, out var owningFolder))
                        throw new ApiExceptionResponse("File does not belong to this project.", 400);
                    if (IsExcludedArea(owningFolder.Area) && !canManageRestrictedAreas)
                        throw new ApiExceptionResponse("Only admin/PM can assign permissions in Published/Archived areas.", 403);
                    // Cổng quyền theo VÙNG của folder chứa file, trên CHÍNH file — khớp phần hiển thị:
                    // Shared cần XEM, WIP cần GHI. Chặn leo thang qua cây.
                    if (!isFullAccess)
                    {
                        var canManageFile = owningFolder.Area == CdeArea.Shared
                            ? await _permissionChecking.HasViewFileAsync(c.TargetId, accountId)
                            : await _permissionChecking.HasEditFileAsync(c.TargetId, accountId);
                        if (!canManageFile)
                            throw new ApiExceptionResponse("You cannot assign permissions on this file.", 403);
                    }
                }
            }

            // Nạp CÓ tracking các dòng hiện có để cập nhật.
            var changedFolderIds = changes
                .Where(c => c.TargetType == MatrixTargetType.Folder)
                .Select(c => c.TargetId).Distinct().ToList();
            var changedParticipantIds = changes.Select(c => c.ProjectParticipantId).Distinct().ToList();

            var existingFolderPerms = (await _matrixRepo
                    .GetFolderPermissionsForUpdateAsync(changedFolderIds, changedParticipantIds))
                .Where(fp => fp.ProjectParticipantId.HasValue)
                .ToDictionary(fp => (fp.FolderId, fp.ProjectParticipantId!.Value));
            var existingFilePerms = (await _matrixRepo
                    .GetFilePermissionsForUpdateAsync(fileChangeIds, changedParticipantIds))
                .Where(fp => fp.ProjectParticipantId.HasValue)
                .ToDictionary(fp => (fp.FileItemId, fp.ProjectParticipantId!.Value));

            var foldersToCreate = new List<FolderPermission>();
            var filesToCreate = new List<FilePermission>();

            foreach (var c in changes)
            {
                if (c.TargetType == MatrixTargetType.Folder)
                {
                    if (existingFolderPerms.TryGetValue((c.TargetId, c.ProjectParticipantId), out var perm))
                    {
                        PermissionLevelMapper.Apply(perm, c.Level, isFile: false);
                    }
                    else if (c.Level is PermissionLevel.Read or PermissionLevel.Write)
                    {
                        // N/Inherit không có dòng cũ = vắng mặt = không quyền -> không cần tạo dòng.
                        var np = new FolderPermission
                        {
                            Id = Guid.NewGuid(),
                            FolderId = c.TargetId,
                            ProjectParticipantId = c.ProjectParticipantId
                        };
                        PermissionLevelMapper.Apply(np, c.Level, isFile: false);
                        foldersToCreate.Add(np);
                    }
                }
                else
                {
                    if (existingFilePerms.TryGetValue((c.TargetId, c.ProjectParticipantId), out var perm))
                    {
                        PermissionLevelMapper.Apply(perm, c.Level, isFile: true);
                    }
                    else if (c.Level != PermissionLevel.Inherit)
                    {
                        // N (chặn) / R / W đều cần dòng override. Inherit không có dòng cũ = giữ kế thừa.
                        var np = new FilePermission
                        {
                            Id = Guid.NewGuid(),
                            FileItemId = c.TargetId,
                            ProjectParticipantId = c.ProjectParticipantId
                        };
                        PermissionLevelMapper.Apply(np, c.Level, isFile: true);
                        filesToCreate.Add(np);
                    }
                }
            }

            if (foldersToCreate.Count > 0)
                await _unitOfWork.Repository<FolderPermission>().CreateRangeAsync(foldersToCreate);
            if (filesToCreate.Count > 0)
                await _unitOfWork.Repository<FilePermission>().CreateRangeAsync(filesToCreate);

            // Mỗi ô là một quyết định phân quyền thật sự -> log phải nói ra đối tượng nào, bên nào,
            // mức nào. "N ô" không truy vết được vì ô trên ma trận không có danh tính khi đọc lại log.
            var groupNames = await PermissionAuditDescriber.ResolveGroupNamesAsync(
                _unitOfWork, changes.Select(c => c.ProjectParticipantId).ToList());
            var auditEntries = changes
                .Select(c =>
                {
                    var targetName = c.TargetType == MatrixTargetType.Folder
                        ? $"thư mục '{(folderById.TryGetValue(c.TargetId, out var f) ? f.Name : c.TargetId.ToString())}'"
                        : $"tệp '{(fileById.TryGetValue(c.TargetId, out var fi) ? fi.Name : c.TargetId.ToString())}'";

                    return PermissionAuditDescriber.Entry(
                        $"{targetName} · {PermissionAuditDescriber.GroupNameOf(groupNames, c.ProjectParticipantId)}",
                        PermissionAuditDescriber.LevelName(c.Level));
                })
                .ToList();

            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.PermissionChange,
                nameof(Project), projectId.ToString(), accountId,
                detail: $"Cập nhật ma trận phân quyền: {PermissionAuditDescriber.Join(auditEntries)}",
                projectId: projectId);

            await _unitOfWork.CommitAsync();

            // [T2] Pool nhóm của các folder/file vừa đổi trên ma trận -> dọn override tài khoản mồ
            // côi (SAU commit để recompute thấy trạng thái mới). File ở đây phòng hờ: ma trận hiện
            // folder-only, nhưng đường lưu vẫn nhận file change nên dọn luôn cho kín.
            foreach (var folderId in changedFolderIds)
                await _cleanup.CleanupFolderOverridesAsync(folderId);
            foreach (var fileId in fileChangeIds)
                await _cleanup.CleanupFileOverridesAsync(fileId);

            return await BuildSaveResultAsync(changes);
        }

        // ===== Helpers =====

        // Lọc hàng ma trận theo tập folder/file đã chọn, GIỮ NGUYÊN thứ tự cây (pre-order).
        // Quy tắc giữ một hàng:
        //  - folder ∈ FolderIds  -> giữ folder đó VÀ toàn bộ hậu duệ (file/subfolder bên trong).
        //  - file   ∈ FileIds    -> giữ file đó.
        //  - tổ tiên của bất kỳ hàng được giữ -> giữ để cây hiển thị đúng đường dẫn (ParentRowId hợp lệ).
        // FolderIds/FileIds kết hợp theo OR. Đã đảm bảo có ít nhất một tập không rỗng trước khi gọi.
        private static List<MatrixRowDTO> ApplyRowFilter(
            List<MatrixRowDTO> rows, List<Guid>? folderIds, List<Guid>? fileIds)
        {
            var folderSet = folderIds is { Count: > 0 } ? folderIds.ToHashSet() : new HashSet<Guid>();
            var fileSet = fileIds is { Count: > 0 } ? fileIds.ToHashSet() : new HashSet<Guid>();

            var rowById = rows.ToDictionary(r => r.TargetId);
            var childrenByParent = rows
                .Where(r => r.ParentRowId.HasValue)
                .GroupBy(r => r.ParentRowId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(r => r.TargetId).ToList());

            var keep = new HashSet<Guid>();

            // Hàng khớp trực tiếp + hậu duệ (BFS) cho folder khớp.
            foreach (var r in rows)
            {
                var directHit =
                    (r.TargetType == MatrixTargetType.Folder && folderSet.Contains(r.TargetId)) ||
                    (r.TargetType == MatrixTargetType.File && fileSet.Contains(r.TargetId));
                if (!directHit) continue;

                var queue = new Queue<Guid>();
                queue.Enqueue(r.TargetId);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    if (!keep.Add(id)) continue;
                    // Chỉ mở rộng hậu duệ khi hàng khớp là folder (file không có con).
                    if (rowById.TryGetValue(id, out var cur) && cur.TargetType == MatrixTargetType.Folder
                        && childrenByParent.TryGetValue(id, out var kids))
                        foreach (var k in kids) queue.Enqueue(k);
                }
            }

            // Bổ sung tổ tiên của mọi hàng được giữ (đi lên theo ParentRowId).
            foreach (var id in keep.ToList())
            {
                var parent = rowById[id].ParentRowId;
                while (parent.HasValue && keep.Add(parent.Value))
                    parent = rowById.TryGetValue(parent.Value, out var pr) ? pr.ParentRowId : null;
            }

            return rows.Where(r => keep.Contains(r.TargetId)).ToList();
        }

        private async Task<List<MatrixCellResultDTO>> BuildSaveResultAsync(List<MatrixCellChangeDTO> changes)
        {
            var folderIds = changes
                .Where(c => c.TargetType == MatrixTargetType.Folder)
                .Select(c => c.TargetId).Distinct().ToList();
            var fileIds = changes
                .Where(c => c.TargetType == MatrixTargetType.File)
                .Select(c => c.TargetId).Distinct().ToList();

            var folderIdByFile = (await _matrixRepo.GetFilesByIdsAsync(fileIds))
                .ToDictionary(f => f.Id, f => f.FolderId);

            var folderPermFolderIds = folderIds.Union(folderIdByFile.Values).Distinct().ToList();
            var folderPermIndex = IndexFolderPerms(
                await _matrixRepo.GetActiveFolderPermissionsByFolderIdsAsync(folderPermFolderIds));
            var filePermIndex = IndexFilePerms(
                await _matrixRepo.GetActiveFilePermissionsByFileIdsAsync(fileIds));

            var results = new List<MatrixCellResultDTO>(changes.Count);
            foreach (var c in changes)
            {
                if (c.TargetType == MatrixTargetType.Folder)
                {
                    results.Add(new MatrixCellResultDTO
                    {
                        TargetId = c.TargetId,
                        TargetType = c.TargetType,
                        ProjectParticipantId = c.ProjectParticipantId,
                        Level = FolderCellLevel(folderPermIndex, c.TargetId, c.ProjectParticipantId),
                        IsInherited = false
                    });
                }
                else
                {
                    var folderId = folderIdByFile.TryGetValue(c.TargetId, out var fid) ? fid : Guid.Empty;
                    var (level, inherited) = ResolveFileCell(
                        filePermIndex, folderPermIndex, c.TargetId, folderId, c.ProjectParticipantId);
                    results.Add(new MatrixCellResultDTO
                    {
                        TargetId = c.TargetId,
                        TargetType = c.TargetType,
                        ProjectParticipantId = c.ProjectParticipantId,
                        Level = level,
                        IsInherited = inherited
                    });
                }
            }
            return results;
        }

        // Published/Archived: nội dung đã phê duyệt/niêm phong — ma trận không quản quyền các vùng này.
        private static bool IsExcludedArea(CdeArea area)
            => area is CdeArea.Published or CdeArea.Archived;

        private static Dictionary<(Guid, Guid), FolderPermission> IndexFolderPerms(List<FolderPermission> perms)
            => perms.Where(p => p.ProjectParticipantId.HasValue)
                    .ToDictionary(p => (p.FolderId, p.ProjectParticipantId!.Value));

        private static Dictionary<(Guid, Guid), FilePermission> IndexFilePerms(List<FilePermission> perms)
            => perms.Where(p => p.ProjectParticipantId.HasValue)
                    .ToDictionary(p => (p.FileItemId, p.ProjectParticipantId!.Value));

        // Ô thư mục: có dòng Active -> N/R/W; không có -> N (thư mục phẳng, mặc định không quyền).
        private static PermissionLevel FolderCellLevel(
            Dictionary<(Guid, Guid), FolderPermission> folderPermIndex, Guid folderId, Guid participantId)
            => folderPermIndex.TryGetValue((folderId, participantId), out var acl)
                ? PermissionLevelMapper.ToLevel(acl)
                : PermissionLevel.NoAccess;

        // Ô file: có override -> lấy trực tiếp (Explicit); không có -> kế thừa quyền thư mục (Inherited).
        private static (PermissionLevel level, bool inherited) ResolveFileCell(
            Dictionary<(Guid, Guid), FilePermission> filePermIndex,
            Dictionary<(Guid, Guid), FolderPermission> folderPermIndex,
            Guid fileId, Guid folderId, Guid participantId)
        {
            if (filePermIndex.TryGetValue((fileId, participantId), out var fileAcl))
                return (PermissionLevelMapper.ToLevel(fileAcl), false);

            return (FolderCellLevel(folderPermIndex, folderId, participantId), true);
        }
    }
}
