using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Cde;
using Domain.Enum.Group;
using Domain.Enum.Permission;
using Domain.Enum.Project;

namespace Application.Services
{
    // Tạo khung thư mục CDE theo ISO 19650. Mọi thao tác idempotent.
    public class FolderBootstrapService : IFolderBootstrapService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        // 4 khu vực gốc + tên hiển thị mặc định.
        private static readonly (CdeArea Area, string Name)[] RootAreas =
        {
            (CdeArea.Wip,       "WIP"),
            (CdeArea.Shared,    "Shared"),
            (CdeArea.Published, "Published"),
            (CdeArea.Archived,  "Archived"),
        };

        public FolderBootstrapService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
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

            var participant = (await _unitOfWork.Repository<ProjectParticipant>()
                    .FindAsync(p => p.ProjectId == projectId
                                 && p.GroupId == groupId
                                 && p.Status == ProjectParticipantStatus.Active))
                .FirstOrDefault()
                ?? throw new ApiExceptionResponse("Group is not an active participant of this project.", 404);

            var roots = await EnsureRootsAsync(projectId);

            // Snapshot folder hiện có của dự án để kiểm tra trùng (cả root mới tạo lẫn con đã có).
            var existing = (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ProjectId == projectId))
                .ToList();

            var now = DateTime.UtcNow;
            var groupFolders = new List<Folder>();

            foreach (var root in roots)
            {
                // Idempotent: đã có "ô" của group dưới root này thì dùng lại, không tạo trùng.
                var folder = existing.FirstOrDefault(f =>
                    f.ParentFolderId == root.Id
                    && string.Equals(f.Name, group.Name, StringComparison.OrdinalIgnoreCase));

                if (folder == null)
                {
                    folder = new Folder
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        ParentFolderId = root.Id,
                        Name = group.Name,
                        Area = root.Area,
                        IsTemplate = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _unitOfWork.Repository<Folder>().CreateAsync(folder);
                }

                groupFolders.Add(folder);
            }

            // Liên kết bản chiếu: "ô" của group ở Shared/Published/Archived trỏ về "ô" WIP.
            var wipGroupFolder = groupFolders.FirstOrDefault(f => f.Area == CdeArea.Wip);
            if (wipGroupFolder != null)
            {
                foreach (var folder in groupFolders)
                {
                    if (folder.Area == CdeArea.Wip || folder.MirrorSourceFolderId != null) continue;

                    folder.MirrorSourceFolderId = wipGroupFolder.Id;
                    if (existing.Contains(folder))
                        _unitOfWork.Repository<Folder>().Update(folder);
                }
            }

            await GrantGroupFolderPermissionsAsync(participant.Id, groupFolders);

            await _unitOfWork.CommitAsync();
        }

        public async Task<FolderResponseDTO> CreateChildFolderAsync(Guid parentFolderId, string name, Guid actor, string? actorRole)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ApiExceptionResponse("Folder name is required.", 400);

            var parent = await _unitOfWork.Repository<Folder>().GetByIdAsync(parentFolderId)
                ?? throw new ApiExceptionResponse("Parent folder not found.", 404);

            // Xác định nhóm sở hữu: parent hoặc tổ tiên gần nhất có OwnerGroupId.
            var ownerGroupId = await ResolveOwnerGroupIdAsync(parent);

            // Phân quyền: Admin hệ thống / PM dự án / Team Leader của nhóm sở hữu.
            await EnsureCanCreateSubFolderAsync(actor, parent.ProjectId, ownerGroupId, actorRole);

            var now = DateTime.UtcNow;
            var child = new Folder
            {
                Id = Guid.NewGuid(),
                ProjectId = parent.ProjectId,
                ParentFolderId = parent.Id,
                Name = name.Trim(),
                Area = parent.Area,                          // kế thừa khu vực
                IsTemplate = false,
                CreatedByAccountId = actor,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _unitOfWork.Repository<Folder>().CreateAsync(child);

            // Kế thừa ACL của folder cha để thành viên đang thấy cha cũng thấy/thao tác được folder con.
            var parentPermissions = await _unitOfWork.Repository<FolderPermission>()
                .FindAsync(p => p.FolderId == parent.Id);
            var childPermissions = new List<FolderPermission>();
            foreach (var permission in parentPermissions)
            {
                var copied = new FolderPermission
                {
                    Id = Guid.NewGuid(),
                    FolderId = child.Id,
                    ProjectParticipantId = permission.ProjectParticipantId,
                    CanView = permission.CanView,
                    CanEdit = permission.CanEdit,
                    CanUpdate = permission.CanUpdate,
                    CanDownload = permission.CanDownload,
                    CanVerify = permission.CanVerify,
                    CanApprove = permission.CanApprove,
                    Status = permission.Status
                };
                childPermissions.Add(copied);
                await _unitOfWork.Repository<FolderPermission>().CreateAsync(copied);
            }

            // Folder tạo trong WIP luôn có "bản chiếu" cùng vị trí ở Shared/Published/Archived.
            if (child.Area == CdeArea.Wip)
                await CreateMirrorFoldersAsync(child, parent, childPermissions, now);

            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderResponseDTO>(child);
        }

        // Cấp quyền mặc định cho participant trên các folder "ô" của group (bỏ qua folder đã có ACL).
        private async Task GrantGroupFolderPermissionsAsync(Guid participantId, IReadOnlyCollection<Folder> folders)
        {
            var folderIds = folders.Select(f => f.Id).ToList();
            var alreadyGranted = (await _unitOfWork.Repository<FolderPermission>()
                    .FindAsync(p => p.ProjectParticipantId == participantId && folderIds.Contains(p.FolderId)))
                .Select(p => p.FolderId)
                .ToHashSet();

            foreach (var folder in folders)
            {
                if (alreadyGranted.Contains(folder.Id)) continue;
                await _unitOfWork.Repository<FolderPermission>()
                    .CreateAsync(BuildDefaultGroupPermission(participantId, folder));
            }
        }

        // WIP: toàn quyền làm việc trên "ô" của chính nhóm; các khu vực còn lại chỉ Xem/Tải
        // (file vào Shared/Published/Archived qua luồng duyệt, không sửa trực tiếp).
        // CanApprove/CanVerify KHÔNG giới hạn theo isWip: file đang chờ duyệt để chuyển từ
        // Shared -> Published (hoặc WIP -> Shared) nằm ở chính "ô" Shared/Published của nhóm, nên
        // Leader nhóm cần quyền Approve tại đó để ResolveFileItemTeamGroupIdsAsync xác định đúng
        // nhóm phụ trách — nếu giới hạn isWip thì mọi approval ở Shared/Published sẽ không tìm
        // được nhóm nào có CanApprove và bị fallback sai về "tất cả các nhóm trong dự án".
        private static FolderPermission BuildDefaultGroupPermission(Guid participantId, Folder folder)
        {
            var isWip = folder.Area == CdeArea.Wip;
            return new FolderPermission
            {
                Id = Guid.NewGuid(),
                FolderId = folder.Id,
                ProjectParticipantId = participantId,
                CanView = true,
                CanEdit = isWip,
                CanUpdate = isWip,
                CanDownload = true,
                CanVerify = true,
                CanApprove = true,
                Status = PermissionStatus.Active
            };
        }

        private async Task<Guid?> ResolveOwnerGroupIdAsync(Folder folder)
        {

            var byId = (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ProjectId == folder.ProjectId))
                .ToDictionary(f => f.Id);

            var cur = folder;
            while (cur.ParentFolderId.HasValue && byId.TryGetValue(cur.ParentFolderId.Value, out var parent))
            {
                
                cur = parent;
            }
            return null;
        }

        private async Task EnsureCanCreateSubFolderAsync(Guid actor, Guid projectId, Guid? ownerGroupId, string? actorRole)
        {
            if (actorRole == AccountRole.Admin.ToString())
                return;

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId);
            if (project?.ManagerAccountId == actor)
                return;

            if (ownerGroupId.HasValue)
            {
                var isLeader = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.GroupId == ownerGroupId.Value
                            && gm.AccountId == actor
                            && gm.Role == GroupMemberRole.Leader))
                    .Any();
                if (isLeader) return;
            }

            throw new ApiExceptionResponse(
                "Only the group's Team Leader (or project manager/Admin) can create sub-folders here.", 403);
        }

        // Các khu vực nhận "bản chiếu" của folder tạo trong WIP.
        private static readonly CdeArea[] MirrorAreas =
        {
            CdeArea.Shared, CdeArea.Published, CdeArea.Archived
        };

        // Tạo bản chiếu của folder WIP mới ở cả 3 khu vực còn lại, đúng vị trí tương ứng.
        private async Task CreateMirrorFoldersAsync(
            Folder wipChild,
            Folder wipParent,
            IReadOnlyCollection<FolderPermission> wipChildPermissions,
            DateTime now)
        {
            var projectFolders = (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ProjectId == wipChild.ProjectId && !f.IsTemplate))
                .ToList();

            foreach (var area in MirrorAreas)
            {
                var zoneRoot = projectFolders.FirstOrDefault(f => f.ParentFolderId == null && f.Area == area);
                if (zoneRoot == null) continue; // khu vực gốc chưa dựng thì bỏ qua

                var mirrorParent = await ResolveMirrorParentAsync(wipParent, area, zoneRoot, projectFolders, now);

                // Idempotent: đã có bản chiếu (theo link hoặc trùng tên cùng vị trí) thì thôi.
                var alreadyMirrored = projectFolders.Any(f =>
                    f.Area == area
                    && (f.MirrorSourceFolderId == wipChild.Id
                        || (f.ParentFolderId == mirrorParent.Id
                            && string.Equals(f.Name, wipChild.Name, StringComparison.OrdinalIgnoreCase))));
                if (alreadyMirrored) continue;

                await CreateMirrorFolderAsync(wipChild, mirrorParent, wipChildPermissions, now, projectFolders);
            }
        }

        // Tìm folder tương ứng của wipParent trong khu vực đích; các cấp trung gian còn thiếu
        // được dựng bù để bản chiếu luôn nằm đúng vị trí như bên WIP.
        private async Task<Folder> ResolveMirrorParentAsync(
            Folder wipParent,
            CdeArea area,
            Folder zoneRoot,
            List<Folder> projectFolders,
            DateTime now)
        {
            if (wipParent.ParentFolderId == null)
                return zoneRoot;

            var byId = projectFolders.ToDictionary(f => f.Id);

            // Chuỗi tổ tiên từ ngay dưới root WIP xuống tới wipParent.
            var pathSegments = new List<Folder>();
            var cur = (Folder?)wipParent;
            while (cur is { ParentFolderId: not null })
            {
                pathSegments.Add(cur);
                byId.TryGetValue(cur.ParentFolderId.Value, out cur);
            }
            pathSegments.Reverse();

            var mirrorParent = zoneRoot;
            foreach (var segment in pathSegments)
            {
                // Ưu tiên khớp theo link mirror (không vỡ khi đổi tên), fallback khớp theo tên cùng vị trí.
                var next = projectFolders.FirstOrDefault(f =>
                               f.Area == area && f.MirrorSourceFolderId == segment.Id)
                           ?? projectFolders.FirstOrDefault(f =>
                               f.Area == area
                               && f.ParentFolderId == mirrorParent.Id
                               && string.Equals(f.Name, segment.Name, StringComparison.OrdinalIgnoreCase));

                if (next == null)
                {
                    var segmentPermissions = (await _unitOfWork.Repository<FolderPermission>()
                            .FindAsync(p => p.FolderId == segment.Id))
                        .ToList();
                    next = await CreateMirrorFolderAsync(segment, mirrorParent, segmentPermissions, now, projectFolders);
                }

                mirrorParent = next;
            }

            return mirrorParent;
        }

        private async Task<Folder> CreateMirrorFolderAsync(
            Folder source,
            Folder mirrorParent,
            IReadOnlyCollection<FolderPermission> sourcePermissions,
            DateTime now,
            List<Folder> projectFolders)
        {
            var mirror = new Folder
            {
                Id = Guid.NewGuid(),
                ProjectId = source.ProjectId,
                ParentFolderId = mirrorParent.Id,
                Name = source.Name,
                Area = mirrorParent.Area,
                IsTemplate = false,
                CreatedByAccountId = source.CreatedByAccountId,
                MirrorSourceFolderId = source.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _unitOfWork.Repository<Folder>().CreateAsync(mirror);
            projectFolders.Add(mirror);

            // Ngoài WIP chỉ Xem/Tải — file vào các khu vực này qua luồng duyệt, không sửa trực tiếp.
            foreach (var permission in sourcePermissions)
            {
                await _unitOfWork.Repository<FolderPermission>().CreateAsync(new FolderPermission
                {
                    Id = Guid.NewGuid(),
                    FolderId = mirror.Id,
                    ProjectParticipantId = permission.ProjectParticipantId,
                    CanView = permission.CanView,
                    CanDownload = permission.CanDownload,
                    CanEdit = false,
                    CanUpdate = false,
                    CanVerify = false,
                    CanApprove = false,
                    Status = permission.Status
                });
            }

            return mirror;
        }

        // Đảm bảo 4 folder gốc tồn tại; trả về danh sách 4 root (cũ + mới, chưa commit).
        private async Task<List<Folder>> EnsureRootsAsync(Guid projectId)
        {
            var projectFolders = (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ProjectId == projectId))
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
                    IsTemplate = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _unitOfWork.Repository<Folder>().CreateAsync(root);
                roots.Add(root);
            }
            
            // Add "00 - Các gói thầu" to Published area
            var publishedRoot = roots.FirstOrDefault(r => r.Area == CdeArea.Published);
            if (publishedRoot != null)
            {
                var packageFolder = projectFolders.FirstOrDefault(f => f.ParentFolderId == publishedRoot.Id && f.Name == "Các gói thầu");
                if (packageFolder == null)
                {
                    await _unitOfWork.Repository<Folder>().CreateAsync(new Folder
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        ParentFolderId = publishedRoot.Id,
                        Name = "Các gói thầu",
                        Area = CdeArea.Published,
                        IsTemplate = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            return roots;
        }
    }
}
