using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Services
{
    // Tạo khung thư mục CDE theo ISO 19650. Mọi thao tác idempotent.
    public class FolderBootstrapService : IFolderBootstrapService
    {
        private readonly IUnitOfWork _unitOfWork;

        // 4 khu vực gốc + tên hiển thị mặc định.
        private static readonly (CdeArea Area, string Name)[] RootAreas =
        {
            (CdeArea.Wip,       "WIP"),
            (CdeArea.Shared,    "Shared"),
            (CdeArea.Published, "Published"),
            (CdeArea.Archived,  "Archived"),
        };

        public FolderBootstrapService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task InitializeRootFoldersAsync(Guid projectId)
        {
            _ = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            await EnsureRootsAsync(projectId);
            await _unitOfWork.CommitAsync();
        }

        public async Task ScaffoldParticipantFoldersAsync(Guid projectId, Guid groupId)
        {
            _ = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var group = await _unitOfWork.Repository<Group>().GetByIdAsync(groupId)
                ?? throw new ApiExceptionResponse("Group not found.", 404);

            var roots = await EnsureRootsAsync(projectId);

            // Snapshot folder hiện có của dự án để kiểm tra trùng (cả root mới tạo lẫn con đã có).
            var existing = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == projectId)
                .ToList();

            var now = DateTime.UtcNow;

            foreach (var root in roots)
            {
                // Đã có "ô" của group này trong khu vực -> bỏ qua.
                var already = existing.Any(f => f.ParentFolderId == root.Id && f.OwnerGroupId == groupId)
                              || roots.Any(r => r.ParentFolderId == root.Id && r.OwnerGroupId == groupId);
                if (already) continue;

                await _unitOfWork.Repository<Folder>().CreateAsync(new Folder
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    ParentFolderId = root.Id,
                    Name = group.Name,
                    Area = root.Area,
                    OwnerGroupId = groupId,
                    OwnerOrganizationId = group.OrganizationId,
                    IsTemplate = false,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _unitOfWork.CommitAsync();
        }

        // Đảm bảo 4 folder gốc tồn tại; trả về danh sách 4 root (cũ + mới, chưa commit).
        private async Task<List<Folder>> EnsureRootsAsync(Guid projectId)
        {
            var projectFolders = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == projectId)
                .ToList();

            var roots = projectFolders
                .Where(f => f.ParentFolderId == null && !f.IsTemplate)
                .ToList();

            var now = DateTime.UtcNow;

            foreach (var (area, name) in RootAreas)
            {
                if (roots.Any(r => r.Area == area)) continue;

                var root = new Folder
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    ParentFolderId = null,
                    Name = name,
                    Area = area,
                    OwnerGroupId = null,
                    OwnerOrganizationId = null,
                    IsTemplate = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _unitOfWork.Repository<Folder>().CreateAsync(root);
                roots.Add(root);
            }

            return roots;
        }
    }
}
