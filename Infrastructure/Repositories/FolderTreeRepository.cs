using Application.Interfaces.IRepositories;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Group;
using Domain.Enum.Permission;
using Domain.Enum.Project;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FolderTreeRepository : GenericRepository<Folder>, IFolderTreeRepository
    {
        private readonly CDESystemDbContext _context;

        public FolderTreeRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        // check project exit or not return true or false
        public async Task<bool> ProjectExistsAsync(Guid projectId)
        {
            return await _context.Projects.AnyAsync(p => p.Id == projectId);
        }

        // get folder by id return only the found folder data(sub data not included) or null
        public async Task<Folder?> GetFolderByIdAsync(Guid folderId)
        {
            return await _context.Folders
                .AsNoTracking()
                .SingleOrDefaultAsync(f => f.Id == folderId);
        }

        // get project folders by project id and area return list of folders
        public async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId, CdeArea? area)
        {
            return await _context.Folders
                .Where(f => f.ProjectId == projectId && !f.IsTemplate) // can coi lai cho nay co muc dich gi
                .Where(f => area == null || f.Area == area.Value)
                .AsNoTracking()
                .ToListAsync();
        }

        // get viewable folder ids by project id and account id return hashset of folder ids
        // used for getting set of folder ids that the user has view permission
        public async Task<HashSet<Guid>> GetViewableFolderIdsAsync(Guid projectId, Guid accountId)
        {
            var folderIds = await _context.FolderPermissions
                .Where(fp => fp.Folder.ProjectId == projectId
                          && fp.Status == PermissionStatus.Active
                          && fp.CanView
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                          && fp.ProjectParticipant.Group.Members.Any(m =>
                                m.AccountId == accountId && m.Status == GroupMemberStatus.Active))
                .Select(fp => fp.FolderId)
                .Distinct()
                .ToListAsync();

            return folderIds.ToHashSet();
        }

        // Các file mà account xem được qua đường quyền RIÊNG TỪNG FILE (FileViewGrant theo tài khoản,
        // hoặc FilePermission override cho phép CanView của nhóm account đang tham gia) NHƯNG folder chứa
        // file lại KHÔNG View được. Đây là các file lẽ ra bị ẩn khỏi cây -> cần "kéo lên" tổ tiên View được.
        // Loại các file nằm trong folder đã View được (chúng hiển thị bình thường dưới folder gốc của mình).
        public async Task<List<FileItem>> GetExtraViewableFilesAsync(
            Guid projectId, Guid accountId, HashSet<Guid> viewableFolderIds)
        {
            // (a) Grant xem theo tài khoản.
            var grantedFileIds = _context.FileViewGrants
                .Where(g => g.AccountId == accountId
                         && g.Status == PermissionStatus.Active
                         && g.FileItem.Folder.ProjectId == projectId)
                .Select(g => g.FileItemId);

            // (b) FilePermission override CHO PHÉP xem (CanView = true) của nhóm account đang là thành viên Active.
            var permittedFileIds = _context.FilePermissions
                .Where(fp => fp.CanView
                          && fp.Status == PermissionStatus.Active
                          && fp.FileItem.Folder.ProjectId == projectId
                          && fp.ProjectParticipant != null
                          && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                          && fp.ProjectParticipant.Group.Members.Any(m =>
                                m.AccountId == accountId && m.Status == GroupMemberStatus.Active))
                .Select(fp => fp.FileItemId);

            var fileIds = await grantedFileIds.Concat(permittedFileIds).Distinct().ToListAsync();
            if (fileIds.Count == 0) return new List<FileItem>();

            // Loại file trong folder đã View được -> tránh trùng với danh sách file hiển thị bình thường.
            return await _context.FileItems
                .Where(fi => fileIds.Contains(fi.Id) && !viewableFolderIds.Contains(fi.FolderId))
                .OrderBy(fi => fi.Name)
                .AsNoTracking()
                .ToListAsync();
        }


        // check if user is admin or not
        public async Task<bool> HasFullAccessAsync(Guid projectId, Guid accountId)
        {
            return await _context.ProjectParticipants
                .AnyAsync(pp => pp.ProjectId == projectId
                             && pp.Status == ProjectParticipantStatus.Active
                             && pp.Role == ProjectParticipantRole.ProjectAdmin
                             && pp.Group.Members.Any(m =>
                                    m.AccountId == accountId && m.Status == GroupMemberStatus.Active));
        }



        // Account là manager của dự án (Project.ManagerAccountId) -> full access như admin hệ thống (kể cả WIP).
        public async Task<bool> IsProjectManagerAsync(Guid projectId, Guid accountId)
        {
            return await _context.Projects
                .AnyAsync(p => p.Id == projectId && p.ManagerAccountId == accountId);
        }


        public async Task<bool> CanViewFolderAsync(Guid folderId, Guid accountId)
        {
            return await _context.FolderPermissions
                .AnyAsync(fp => fp.FolderId == folderId
                             && fp.Status == PermissionStatus.Active
                             && fp.CanView
                             && fp.ProjectParticipant != null
                             && fp.ProjectParticipant.Status == ProjectParticipantStatus.Active
                             && fp.ProjectParticipant.Group.Members.Any(m =>
                                    m.AccountId == accountId && m.Status == GroupMemberStatus.Active));
        }

        public async Task<List<FileItem>> GetFilesByFolderIdAsync(Guid folderId)
        {
            return await _context.FileItems
                .Where(fi => fi.FolderId == folderId)
                .OrderBy(fi => fi.Name)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<HashSet<Guid>> GetWarningFolderIdsAsync(Guid projectId)
        {
            // Cảnh báo giờ ở version hiện hành -> folder "có cảnh báo" = có file mà bản hiện hành bị Warnning.
            var ids = await _context.Set<FileVersionState>()
                .Where(v => v.IsCurrent && v.Warnning == true && v.FileItem.Folder.ProjectId == projectId)
                .Select(v => v.FileItem.FolderId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        public async Task<List<Folder>> GetChildFoldersAsync(Guid parentFolderId)
        {
            return await _context.Folders
                .Where(f => f.ParentFolderId == parentFolderId && !f.IsTemplate)
                .OrderBy(f => f.Name)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
