using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Cde;

namespace Application.Services
{
    public class FolderPermissionService : IFolderPermissionService
    {
        private const string SurveyorTypeCode = "Surveyor";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        public FolderPermissionService(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _mapper = mapper;
        }

        // Bối cảnh của 1 account trong 1 dự án — tính 1 lần, tái dùng cho mọi folder.
        private sealed class UserContext
        {
            public bool IsAdmin { get; init; }
            public bool IsManager { get; init; }
            public bool IsParticipant { get; init; }
            public bool IsSurveyor { get; init; }
            public HashSet<Guid> GroupIds { get; init; } = new();
            public HashSet<Guid> OrgIds { get; init; } = new();
        }

        public async Task<EffectivePermissionDTO> EvaluateAsync(Guid accountId, Guid folderId)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            var ctx = await BuildContextAsync(accountId, folder.ProjectId);

            var foldersById = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == folder.ProjectId)
                .ToDictionary(f => f.Id);

            var permsByFolder = (await _unitOfWork.Repository<FolderPermission>().GetAllAsync())
                .Where(p => foldersById.ContainsKey(p.FolderId))
                .GroupBy(p => p.FolderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return EvaluateCore(folder, ctx, foldersById, permsByFolder);
        }

        public async Task RequireAsync(Guid accountId, Guid folderId, FolderAction action)
        {
            var perm = await EvaluateAsync(accountId, folderId);
            var allowed = action switch
            {
                FolderAction.View => perm.CanView,
                FolderAction.Edit => perm.CanEdit,
                FolderAction.Update => perm.CanUpdate,
                FolderAction.Download => perm.CanDownload,
                FolderAction.Verify => perm.CanVerify,
                FolderAction.Approve => perm.CanApprove,
                _ => false
            };
            if (!allowed)
                throw new ApiExceptionResponse($"You do not have '{action}' permission on this folder.", 403);
        }

        public async Task<List<FolderTreeNodeDTO>> GetTreeAsync(Guid projectId, Guid accountId, CdeArea? area = null)
        {
            _ = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var ctx = await BuildContextAsync(accountId, projectId);

            var folders = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == projectId && !f.IsTemplate)
                .Where(f => area == null || f.Area == area.Value)
                .ToList();

            var foldersById = folders.ToDictionary(f => f.Id);

            var permsByFolder = (await _unitOfWork.Repository<FolderPermission>().GetAllAsync())
                .Where(p => foldersById.ContainsKey(p.FolderId))
                .GroupBy(p => p.FolderId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Quyền hiệu lực cho mọi folder, rồi chỉ giữ folder người gọi được View.
            var eff = folders.ToDictionary(f => f.Id, f => EvaluateCore(f, ctx, foldersById, permsByFolder));
            var visible = folders.Where(f => eff[f.Id].CanView).ToList();
            var visibleIds = visible.Select(f => f.Id).ToHashSet();

            var nodes = visible.ToDictionary(f => f.Id, f => new FolderTreeNodeDTO
            {
                Id = f.Id,
                ProjectId = f.ProjectId,
                ParentFolderId = f.ParentFolderId,
                Name = f.Name,
                Area = f.Area,
                Permission = eff[f.Id]
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

        public async Task<List<FolderPermissionResponseDTO>> GetPermissionsAsync(Guid folderId)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            await EnsureCanManageAsync(folder);

            var rows = (await _unitOfWork.Repository<FolderPermission>().GetAllAsync())
                .Where(p => p.FolderId == folderId)
                .ToList();

            return rows.Select(_mapper.Map<FolderPermissionResponseDTO>).ToList();
        }

        public async Task<FolderPermissionResponseDTO> SetPermissionAsync(Guid folderId, SetFolderPermissionDTO dto)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            await EnsureCanManageAsync(folder);

            var hasGroup = dto.GroupId.HasValue && dto.GroupId.Value != Guid.Empty;
            var hasOrg = dto.OrganizationId.HasValue && dto.OrganizationId.Value != Guid.Empty;
            if (hasGroup == hasOrg)
                throw new ApiExceptionResponse("Provide exactly one of GroupId or OrganizationId.", 400);

            // Upsert theo (FolderId, GroupId|OrganizationId). Lấy entity đang được EF theo dõi để
            // mutate trực tiếp + Commit (tránh GenericRepository.Update vốn clear ChangeTracker).
            var existing = (await _unitOfWork.Repository<FolderPermission>().GetAllAsync())
                .FirstOrDefault(p => p.FolderId == folderId
                                  );

            if (existing == null)
            {
                existing = new FolderPermission
                {
                    Id = Guid.NewGuid(),
                    FolderId = folderId,
                };
                ApplyFlags(existing, dto);
                await _unitOfWork.Repository<FolderPermission>().CreateAsync(existing);
            }
            else
            {
                ApplyFlags(existing, dto);
            }

            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderPermissionResponseDTO>(existing);
        }

        public async Task DeletePermissionAsync(Guid folderId, Guid permissionId)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            await EnsureCanManageAsync(folder);

            var row = (await _unitOfWork.Repository<FolderPermission>().GetAllAsync())
                .FirstOrDefault(p => p.Id == permissionId && p.FolderId == folderId)
                ?? throw new ApiExceptionResponse("Permission not found.", 404);

            _unitOfWork.Repository<FolderPermission>().Delete(row);
            await _unitOfWork.CommitAsync();
        }

        // ---------- nội bộ ----------

        // Chỉ Admin hệ thống hoặc PM của dự án mới được quản lý ACL thư mục.
        private async Task EnsureCanManageAsync(Folder folder)
        {
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            if (_currentUser.SystemRole == AccountRole.Admin.ToString())
                return;

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(folder.ProjectId);
            if (project?.ManagerAccountId == actor)
                return;

            throw new ApiExceptionResponse("Only Admin or the project manager can manage folder permissions.", 403);
        }

        private static void ApplyFlags(FolderPermission p, SetFolderPermissionDTO dto)
        {
            p.CanView = dto.CanView;
            p.CanEdit = dto.CanEdit;
            p.CanUpdate = dto.CanUpdate;
            p.CanDownload = dto.CanDownload;
            p.CanVerify = dto.CanVerify;
            p.CanApprove = dto.CanApprove;
            p.InheritFromParent = dto.InheritFromParent;
        }

        private async Task<UserContext> BuildContextAsync(Guid accountId, Guid projectId)
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId);
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId);

            var isAdmin = account?.Role == AccountRole.Admin;
            var isManager = project?.ManagerAccountId == accountId;

            // Group account thuộc về (toàn hệ thống).
            var myAllGroupIds = (await _unitOfWork.Repository<GroupMember>().GetAllAsync())
                .Where(gm => gm.AccountId == accountId)
                .Select(gm => gm.GroupId)
                .ToHashSet();

            // Group đang là bên tham gia của ĐÚNG dự án này.
            var projectGroupIds = (await _unitOfWork.Repository<ProjectParticipant>().GetAllAsync())
                .Where(pp => pp.ProjectId == projectId)
                .Select(pp => pp.GroupId)
                .ToHashSet();

            var myProjectGroupIds = myAllGroupIds.Where(projectGroupIds.Contains).ToHashSet();

            // Tổ chức của account trong dự án (qua Group.OrganizationId).
            var myOrgIds = (await _unitOfWork.Repository<Group>().GetAllAsync())
                .Where(g => myProjectGroupIds.Contains(g.Id) && g.OrganizationId.HasValue)
                .Select(g => g.OrganizationId!.Value)
                .ToHashSet();

            // Có thuộc đơn vị loại "Tư vấn giám sát" không?
            var isSurveyor = false;
            if (myOrgIds.Count > 0)
            {
                var surveyorTypeId = (await _unitOfWork.Repository<OrganizationType>().GetAllAsync())
                    .FirstOrDefault(t => t.Code == SurveyorTypeCode)?.Id;
                if (surveyorTypeId.HasValue)
                {
                    isSurveyor = (await _unitOfWork.Repository<Organization>().GetAllAsync())
                        .Any(o => myOrgIds.Contains(o.Id) && o.OrganizationTypeId == surveyorTypeId.Value);
                }
            }

            return new UserContext
            {
                IsAdmin = isAdmin,
                IsManager = isManager,
                IsParticipant = myProjectGroupIds.Count > 0,
                IsSurveyor = isSurveyor,
                GroupIds = myProjectGroupIds,
                OrgIds = myOrgIds
            };
        }

        private static EffectivePermissionDTO EvaluateCore(
            Folder folder,
            UserContext ctx,
            Dictionary<Guid, Folder> foldersById,
            Dictionary<Guid, List<FolderPermission>> permsByFolder)
        {
            // Bypass: Admin hệ thống và PM của dự án có toàn quyền.
            if (ctx.IsAdmin || ctx.IsManager)
                return Full(folder.Id);

            var isOwner = true;
                

            var p = Baseline(folder, isOwner, ctx.IsParticipant, ctx.IsSurveyor);

            // Override tường minh trên chính folder.
            if (permsByFolder.TryGetValue(folder.Id, out var ownRows))
                foreach (var r in ownRows)
                    if (TargetsUser(r, ctx)) Merge(p, r);

            // Override từ tổ tiên có cờ InheritFromParent (lan xuống con).
            var current = folder;
            while (current.ParentFolderId.HasValue
                   && foldersById.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                if (permsByFolder.TryGetValue(parent.Id, out var pRows))
                    foreach (var r in pRows)
                        if (r.InheritFromParent && TargetsUser(r, ctx)) Merge(p, r);
                current = parent;
            }

            return p;
        }

        // Baseline theo nghiệp vụ ISO 19650 khi chưa có override.
        private static EffectivePermissionDTO Baseline(Folder folder, bool isOwner, bool isParticipant, bool isSurveyor)
        {
            var p = new EffectivePermissionDTO { FolderId = folder.Id };

            // Ngoài dự án và không sở hữu -> không thấy gì.
            if (!isParticipant && !isOwner) return p;

            // Folder gốc (WIP/Shared/...) chỉ là "ngăn" điều hướng -> cho participant thấy để duyệt xuống con.
            if (folder.ParentFolderId == null)
            {
                p.CanView = true;
                return p;
            }

            switch (folder.Area)
            {
                case CdeArea.Wip:
                    // Riêng tư của đơn vị: chỉ chủ sở hữu mới thấy & sửa.
                    if (isOwner) { p.CanView = p.CanEdit = p.CanUpdate = p.CanDownload = true; }
                    break;

                case CdeArea.Shared:
                    if (isOwner) { p.CanView = p.CanEdit = p.CanUpdate = p.CanDownload = true; }
                    else if (isParticipant)
                    {
                        p.CanView = p.CanDownload = true;
                        if (isSurveyor) p.CanVerify = true;   // TVGS thẩm tra ở khu Shared
                    }
                    break;

                case CdeArea.Published:
                case CdeArea.Archived:
                    // Thông tin chính thức / lưu trữ: mọi bên xem & tải, không sửa.
                    if (isOwner || isParticipant) { p.CanView = p.CanDownload = true; }
                    break;
            }

            return p;
        }

        private static bool TargetsUser(FolderPermission r, UserContext ctx)
            => true
            ;

        private static void Merge(EffectivePermissionDTO p, FolderPermission r)
        {
            p.CanView |= r.CanView;
            p.CanEdit |= r.CanEdit;
            p.CanUpdate |= r.CanUpdate;
            p.CanDownload |= r.CanDownload;
            p.CanVerify |= r.CanVerify;
            p.CanApprove |= r.CanApprove;
        }

        private static EffectivePermissionDTO Full(Guid folderId) => new()
        {
            FolderId = folderId,
            CanView = true,
            CanEdit = true,
            CanUpdate = true,
            CanDownload = true,
            CanVerify = true,
            CanApprove = true
        };

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
