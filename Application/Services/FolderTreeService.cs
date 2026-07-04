using Application.DTOs.ResponseDTOs.FileItem;
using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using AutoMapper;
using Domain.Enum.Cde;

namespace Application.Services
{
    public class FolderTreeService : IFolderTreeService
    {
        private readonly IFolderTreeRepository _folderTreeRepository;
        private readonly IMapper _mapper;

        public FolderTreeService(IFolderTreeRepository folderTreeRepository, IMapper mapper)
        {
            _folderTreeRepository = folderTreeRepository;
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
                visible = folders.Where(f => viewableIds.Contains(f.Id)).ToList();
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
            foreach (var f in visible)
            {
                var node = nodes[f.Id];
                if (f.ParentFolderId.HasValue && visibleIds.Contains(f.ParentFolderId.Value))
                    nodes[f.ParentFolderId.Value].Children.Add(node);
                else
                    roots.Add(node);
            }

            SortRecursive(roots);
            return roots;
        }

        public async Task<List<FileItemResponseDTO>> GetFilesByFolderAsync(Guid folderId, Guid accountId, bool isSystemAdmin)
        {
            var folder = await _folderTreeRepository.GetFolderByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            // Quyền View kiểm tra tại thời điểm click vào folder, không kiểm tra sẵn trên cây.
            var canView = isSystemAdmin
                || await _folderTreeRepository.HasFullAccessAsync(folder.ProjectId, accountId)
                || await _folderTreeRepository.CanViewFolderAsync(folderId, accountId);

            if (!canView)
                throw new ApiExceptionResponse("You do not have permission to view this folder.", 403);

            var files = await _folderTreeRepository.GetFilesByFolderIdAsync(folderId);
            return _mapper.Map<List<FileItemResponseDTO>>(files);
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
