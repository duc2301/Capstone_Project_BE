using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Cde;
using Domain.Enum.Group;

namespace Application.Services
{
    // Tạo khung thư mục CDE theo ISO 19650. Mọi thao tác idempotent.
    public class FolderBootstrapService : IFolderBootstrapService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        // 4 khu vực gốc + tên hiển thị mặc định.
        private static readonly (CdeArea Area, string Name)[] RootAreas =
        {
            (CdeArea.Wip,       "WIP"),
            (CdeArea.Shared,    "Shared"),
            (CdeArea.Published, "Published"),
            (CdeArea.Archived,  "Archived"),
        };

        public FolderBootstrapService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _mapper = mapper;
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

        public async Task<FolderResponseDTO> CreateChildFolderAsync(Guid parentFolderId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ApiExceptionResponse("Folder name is required.", 400);

            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var parent = await _unitOfWork.Repository<Folder>().GetByIdAsync(parentFolderId)
                ?? throw new ApiExceptionResponse("Parent folder not found.", 404);

            // Xác định nhóm sở hữu: parent hoặc tổ tiên gần nhất có OwnerGroupId.
            var ownerGroupId = await ResolveOwnerGroupIdAsync(parent);

            // Phân quyền: Admin hệ thống / PM dự án / Team Leader của nhóm sở hữu.
            await EnsureCanCreateSubFolderAsync(actor, parent.ProjectId, ownerGroupId);

            var now = DateTime.UtcNow;
            var child = new Folder
            {
                Id = Guid.NewGuid(),
                ProjectId = parent.ProjectId,
                ParentFolderId = parent.Id,
                Name = name.Trim(),
                Area = parent.Area,                          // kế thừa khu vực
                OwnerGroupId = ownerGroupId,                 // kế thừa chủ sở hữu
                OwnerOrganizationId = parent.OwnerOrganizationId,
                IsTemplate = false,
                CreatedByAccountId = actor,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _unitOfWork.Repository<Folder>().CreateAsync(child);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderResponseDTO>(child);
        }

        private async Task<Guid?> ResolveOwnerGroupIdAsync(Folder folder)
        {
            if (folder.OwnerGroupId.HasValue) return folder.OwnerGroupId;

            var byId = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == folder.ProjectId)
                .ToDictionary(f => f.Id);

            var cur = folder;
            while (cur.ParentFolderId.HasValue && byId.TryGetValue(cur.ParentFolderId.Value, out var parent))
            {
                if (parent.OwnerGroupId.HasValue) return parent.OwnerGroupId;
                cur = parent;
            }
            return null;
        }

        private async Task EnsureCanCreateSubFolderAsync(Guid actor, Guid projectId, Guid? ownerGroupId)
        {
            if (_currentUser.SystemRole == AccountRole.Admin.ToString())
                return;

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId);
            if (project?.ManagerAccountId == actor)
                return;

            if (ownerGroupId.HasValue)
            {
                var isLeader = (await _unitOfWork.Repository<GroupMember>().GetAllAsync())
                    .Any(gm => gm.GroupId == ownerGroupId.Value
                            && gm.AccountId == actor
                            && gm.Role == GroupMemberRole.Leader);
                if (isLeader) return;
            }

            throw new ApiExceptionResponse(
                "Only the group's Team Leader (or project manager/Admin) can create sub-folders here.", 403);
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
