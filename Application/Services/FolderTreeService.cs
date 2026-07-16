using Application.DTOs.ResponseDTOs.FileItem;
using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Services
{
    public class FolderTreeService : IFolderTreeService
    {
        private readonly IFolderTreeRepository _folderTreeRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FolderTreeService(IFolderTreeRepository folderTreeRepository, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _folderTreeRepository = folderTreeRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<List<FolderTreeNodeDTO>> GetTreeAsync(Guid projectId, Guid accountId, bool isSystemAdmin, CdeArea? area = null)
        {
            if (!await _folderTreeRepository.ProjectExistsAsync(projectId))
                throw new ApiExceptionResponse("Project not found.", 404);

            var folders = await _folderTreeRepository.GetProjectFoldersAsync(projectId, area);

            // Admin hệ thống / PM của dự án thấy toàn bộ cây; còn lại chỉ thấy folder có quyền View.
            var hasFullAccess = isSystemAdmin || await _folderTreeRepository.HasFullAccessAsync(projectId, accountId);

            var visible = folders;
            if (!hasFullAccess)
            {
                var viewableIds = await _folderTreeRepository.GetViewableFolderIdsAsync(projectId, accountId);
                // 4 khu vực gốc (WIP/Shared/Published/Archived) luôn hiển thị với mọi thành viên;
                // các folder con vẫn lọc theo quyền View như cũ.
                visible = folders.Where(f => f.ParentFolderId == null || viewableIds.Contains(f.Id)).ToList();
            }

            var visibleIds = visible.Select(f => f.Id).ToHashSet();

            var nodes = visible.ToDictionary(f => f.Id, f => new FolderTreeNodeDTO
            {
                Id = f.Id,
                ProjectId = f.ProjectId,
                ParentFolderId = f.ParentFolderId,
                Name = f.Name,
                Area = f.Area
            });

            var roots = new List<FolderTreeNodeDTO>();
            var folderById = folders.ToDictionary(f => f.Id);
            // Cha hiển thị thực tế của mỗi node (có thể khác cha thật khi cha thật bị ẩn).
            var displayParentById = new Dictionary<Guid, Guid>();

            // forming tree structure by linking children to their parents
            // Folder được View nhưng cha bị ẩn: gắn vào tổ tiên gần nhất còn hiển thị
            // (tối thiểu là khu vực gốc WIP/Shared/... — luôn hiển thị) thay vì đẩy lên cùng cấp với gốc.
            foreach (var f in visible)
            {
                var node = nodes[f.Id];
                var anchorId = FindNearestVisibleAncestorId(f, folderById, visibleIds);
                if (anchorId.HasValue)
                {
                    nodes[anchorId.Value].Children.Add(node);
                    displayParentById[f.Id] = anchorId.Value;
                }
                else
                {
                    roots.Add(node);
                }
            }

            var warningFolderIds = await _folderTreeRepository.GetWarningFolderIdsAsync(projectId);
            foreach (var folderId in warningFolderIds)
            {
                var cur = folderId;
                while (nodes.TryGetValue(cur, out var node) && !node.HasWarning)
                {
                    node.HasWarning = true;
                    if (!displayParentById.TryGetValue(cur, out var parentId)) break;
                    cur = parentId;
                }
            }

            SortRecursive(roots);
            return roots;
        }

        // get files in a folder, with permission checks
        public async Task<List<FileItemResponseDTO>> GetFilesByFolderAsync(Guid folderId, Guid accountId, bool isSystemAdmin)
        {
            var folder = await _folderTreeRepository.GetFolderByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            // Quyền View kiểm tra tại thời điểm click vào folder, không kiểm tra sẵn trên cây.
            var canView = isSystemAdmin
                || await _folderTreeRepository.HasFullAccessAsync(folder.ProjectId, accountId)
                || await _folderTreeRepository.CanViewFolderAsync(folderId, accountId);

            if (!canView)
            {
                // Khu vực gốc luôn mở được: trả danh sách rỗng thay vì 403.
                if (folder.ParentFolderId == null)
                    return new List<FileItemResponseDTO>();

                throw new ApiExceptionResponse("You do not have permission to view this folder.", 403);
            }

            var files = await _folderTreeRepository.GetFilesByFolderIdAsync(folderId);
            return await EnrichWithVersionInfoAsync(_mapper.Map<List<FileItemResponseDTO>>(files));
        }

        // get folder contents (subfolders + files) for a given folder, with permission checks
        public async Task<FolderContentsDTO> GetFolderContentsAsync(Guid folderId, Guid accountId, bool isSystemAdmin)
        {
            var folder = await _folderTreeRepository.GetFolderByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            // Quyền View của chính folder được click — kiểm tra tại thời điểm click.
            var hasFullAccess = isSystemAdmin
                || await _folderTreeRepository.HasFullAccessAsync(folder.ProjectId, accountId);

            var canViewFolder = hasFullAccess
                || await _folderTreeRepository.CanViewFolderAsync(folderId, accountId);

            // 4 khu vực gốc luôn mở được (trả nội dung đã lọc theo quyền) thay vì 403;
            // folder con không có quyền View vẫn bị chặn như cũ.
            if (!canViewFolder && folder.ParentFolderId != null)
                throw new ApiExceptionResponse("You do not have permission to view this folder.", 403);

            var children = await _folderTreeRepository.GetChildFoldersAsync(folderId);

            // Subfolder cũng lọc theo quyền View — user chỉ thấy nhánh mình được phép vào.
            if (!hasFullAccess)
            {
                var viewableIds = await _folderTreeRepository.GetViewableFolderIdsAsync(folder.ProjectId, accountId);
                children = children.Where(f => viewableIds.Contains(f.Id)).ToList();
            }

            var subfolders = children.Select(f => new FolderTreeNodeDTO
            {
                Id = f.Id,
                ProjectId = f.ProjectId,
                ParentFolderId = f.ParentFolderId,
                Name = f.Name,
                Area = f.Area
            }).ToList();
            SortRecursive(subfolders);

            // Không có quyền View trên khu vực gốc thì ẩn file, chỉ thấy subfolder được phép.
            var files = canViewFolder
                ? await _folderTreeRepository.GetFilesByFolderIdAsync(folderId)
                : new List<Domain.Entities.FileItem>();

            return new FolderContentsDTO
            {
                Id = folderId,
                Subfolders = subfolders,
                Files = await EnrichWithVersionInfoAsync(_mapper.Map<List<FileItemResponseDTO>>(files))
            };
        }

        // Gộp thông tin version hiện hành (DisplayVersion "P01.02"/"C01") + email người upload
        // vào danh sách file — batch 2 query, không query từng file.
        private async Task<List<FileItemResponseDTO>> EnrichWithVersionInfoAsync(List<FileItemResponseDTO> files)
        {
            var versionIds = files
                .Where(f => f.CurrentVersionId.HasValue)
                .Select(f => f.CurrentVersionId!.Value)
                .Distinct()
                .ToList();
            if (versionIds.Count == 0) return files;

            var versionsById = (await _unitOfWork.Repository<FileVersionState>()
                    .FindAsync(v => versionIds.Contains(v.Id)))
                .ToDictionary(v => v.Id);

            var uploaderIds = versionsById.Values
                .Where(v => v.UploadedByAccountId.HasValue)
                .Select(v => v.UploadedByAccountId!.Value)
                .Distinct()
                .ToList();
            var emailsByAccountId = uploaderIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Account>().FindAsync(a => uploaderIds.Contains(a.Id)))
                    .ToDictionary(a => a.Id, a => a.Email);

            foreach (var file in files)
            {
                if (!file.CurrentVersionId.HasValue
                    || !versionsById.TryGetValue(file.CurrentVersionId.Value, out var version))
                    continue;

                file.DisplayVersion = version.DisplayVersion;
                file.FileSizeBytes = version.FileSizeBytes;
                file.UploaderEmail = version.UploadedByAccountId.HasValue
                    && emailsByAccountId.TryGetValue(version.UploadedByAccountId.Value, out var email)
                    ? email
                    : null;
            }

            return files;
        }

        // Tìm tổ tiên gần nhất còn hiển thị của folder (đi ngược ParentFolderId trên toàn bộ
        // folder của dự án, kể cả folder bị ẩn). Trả null nếu là folder gốc / không còn tổ tiên nào hiển thị.
        private static Guid? FindNearestVisibleAncestorId(
            Folder folder,
            IReadOnlyDictionary<Guid, Folder> folderById,
            HashSet<Guid> visibleIds)
        {
            var seen = new HashSet<Guid>();
            var parentId = folder.ParentFolderId;

            while (parentId.HasValue && seen.Add(parentId.Value))
            {
                if (visibleIds.Contains(parentId.Value))
                    return parentId;

                parentId = folderById.TryGetValue(parentId.Value, out var parent)
                    ? parent.ParentFolderId
                    : null;
            }

            return null;
        }

        private static void SortRecursive(List<FolderTreeNodeDTO> nodes)
        {
            nodes.Sort((a, b) =>
            {
                var byArea = a.Area.CompareTo(b.Area);
                return byArea != 0 ? byArea : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            foreach (var n in nodes) SortRecursive(n.Children);
        }
    }
}
