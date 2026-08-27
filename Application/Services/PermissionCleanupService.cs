using Application.DTOs.ResponseDTOs.Permission;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// Part 4 của redesign quyền: dọn các dòng override THEO TÀI KHOẢN đã "mồ côi" — tài khoản không
    /// còn thuộc nhóm nào cấp View trên tài nguyên đó. Dưới mô hình mask (Part 1), override chỉ tinh
    /// chỉnh bên trong quyền nhóm, nên dòng mồ côi là dòng chết (trần nhóm đã chặn trước) — dọn là
    /// vệ sinh dữ liệu, không phải chốt an ninh. Vì thế chạy SAU commit của mutation gây ra nó
    /// (recompute đọc DB) và commit riêng: lỡ fail giữa chừng chỉ để lại dòng trơ, lần chạy sau dọn nốt.
    ///
    /// Quyết định "còn trong pool hay không" dùng ĐÚNG phép tính của roster
    /// (FilePermissionService.GetMemberPermissionsAsync): file = grant nhóm trên file (present-wins,
    /// kể cả deny) ∪ grant View của thư mục cha TRỰC TIẾP; folder = grant View trên chính folder đó.
    /// XÓA CỨNG (không Inactive): deny tài khoản được lưu Status=Active (gotcha isFile:true), nên
    /// Inactive không dùng làm cờ "đã dọn" được; re-share sau này cũng không nên hồi sinh tinh chỉnh cũ.
    /// KHÔNG đụng FileViewGrant / issue-stakeholder — đó là đường cấp View cộng thêm ngoài nhóm.
    /// </summary>
    public class PermissionCleanupService : IPermissionCleanupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PermissionCleanupService> _logger;

        public PermissionCleanupService(IUnitOfWork unitOfWork, ILogger<PermissionCleanupService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<int> CleanupFileOverridesAsync(Guid fileItemId)
        {
            var rows = (await _unitOfWork.Repository<FilePermission>()
                    .FindAsync(fp => fp.FileItemId == fileItemId && fp.AccountId != null))
                .ToList();
            if (rows.Count == 0) return 0;

            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (file == null) return 0;

            var pool = await GetFilePoolAccountIdsAsync(fileItemId, file.FolderId, folderGrants: null);

            var deleted = DeleteOutOfPool(rows, r => r.AccountId!.Value, pool);
            return await CommitIfAnyAsync(deleted, nameof(FileItem), fileItemId);
        }

        public async Task<int> CleanupFolderOverridesAsync(Guid folderId)
        {
            var deleted = 0;

            // Pool của chính folder = các nhóm đang cấp View trên folder đó (không xét tổ tiên —
            // ACL nhóm không kế thừa, khớp roster của folder).
            var folderGrants = await _unitOfWork.FolderPermissionRepository
                .GetActiveGroupGrantsByFolderIdAsync(folderId);

            var folderRows = (await _unitOfWork.Repository<FolderPermission>()
                    .FindAsync(fp => fp.FolderId == folderId && fp.AccountId != null))
                .ToList();
            if (folderRows.Count > 0)
            {
                var folderPool = await GetMemberAccountIdsAsync(
                    folderGrants.Where(g => g.CanView).Select(g => g.ParticipantId).ToList());
                deleted += DeleteOutOfPool(folderRows, r => r.AccountId!.Value, folderPool);
            }

            // File con TRỰC TIẾP: chỉ chúng fallback về ACL của folder này. Pool từng file vẫn phải
            // tính riêng vì file có thể mang override nhóm riêng (present-wins).
            var childFileIds = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(fi => fi.FolderId == folderId))
                .Select(fi => fi.Id)
                .ToList();
            if (childFileIds.Count > 0)
            {
                var fileRows = (await _unitOfWork.Repository<FilePermission>()
                        .FindAsync(fp => fp.AccountId != null && childFileIds.Contains(fp.FileItemId)))
                    .ToList();

                foreach (var group in fileRows.GroupBy(r => r.FileItemId))
                {
                    var pool = await GetFilePoolAccountIdsAsync(group.Key, folderId, folderGrants);
                    deleted += DeleteOutOfPool(group.ToList(), r => r.AccountId!.Value, pool);
                }
            }

            return await CommitIfAnyAsync(deleted, nameof(Folder), folderId);
        }

        public async Task<int> CleanupAccountOverridesAsync(Guid accountId)
        {
            var deleted = 0;

            var fileRows = (await _unitOfWork.Repository<FilePermission>()
                    .FindAsync(fp => fp.AccountId == accountId))
                .ToList();
            foreach (var row in fileRows)
            {
                var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(row.FileItemId);
                if (file == null) continue;
                var pool = await GetFilePoolAccountIdsAsync(row.FileItemId, file.FolderId, folderGrants: null);
                if (!pool.Contains(accountId))
                {
                    _unitOfWork.Repository<FilePermission>().Delete(row);
                    deleted++;
                }
            }

            var folderRows = (await _unitOfWork.Repository<FolderPermission>()
                    .FindAsync(fp => fp.AccountId == accountId))
                .ToList();
            foreach (var row in folderRows)
            {
                var grants = await _unitOfWork.FolderPermissionRepository
                    .GetActiveGroupGrantsByFolderIdAsync(row.FolderId);
                var pool = await GetMemberAccountIdsAsync(
                    grants.Where(g => g.CanView).Select(g => g.ParticipantId).ToList());
                if (!pool.Contains(accountId))
                {
                    _unitOfWork.Repository<FolderPermission>().Delete(row);
                    deleted++;
                }
            }

            return await CommitIfAnyAsync(deleted, nameof(Account), accountId);
        }

        // ===== Pool =====

        /// <summary>
        /// Tập tài khoản còn trong pool View của file: grant nhóm trên file present-wins (deny trên
        /// file loại nhóm đó dù folder có cấp View), nhóm không có dòng file thì kế thừa grant View
        /// của thư mục cha trực tiếp. folderGrants truyền vào để dùng lại khi dọn cả loạt file cùng
        /// một folder; null thì tự nạp.
        /// </summary>
        private async Task<HashSet<Guid>> GetFilePoolAccountIdsAsync(
            Guid fileItemId, Guid folderId, List<GroupGrantDTO>? folderGrants)
        {
            var fileGrants = await _unitOfWork.FilePermissionRepository
                .GetActiveGroupGrantsByFileItemIdAsync(fileItemId);
            folderGrants ??= await _unitOfWork.FolderPermissionRepository
                .GetActiveGroupGrantsByFolderIdAsync(folderId);

            var effective = new Dictionary<Guid, GroupGrantDTO>();
            foreach (var g in fileGrants) effective[g.ParticipantId] = g;                 // present-wins
            foreach (var g in folderGrants)
                if (!effective.ContainsKey(g.ParticipantId))
                    effective[g.ParticipantId] = g;                                        // kế thừa folder

            return await GetMemberAccountIdsAsync(
                effective.Where(kv => kv.Value.CanView).Select(kv => kv.Key).ToList());
        }

        private async Task<HashSet<Guid>> GetMemberAccountIdsAsync(List<Guid> participantIds)
        {
            if (participantIds.Count == 0) return new HashSet<Guid>();
            var members = await _unitOfWork.FilePermissionRepository
                .GetActiveMembersByParticipantIdsAsync(participantIds);
            return members.Select(m => m.AccountId).ToHashSet();
        }

        // ===== Delete/commit =====

        private int DeleteOutOfPool<T>(List<T> rows, Func<T, Guid> accountOf, HashSet<Guid> pool)
            where T : class
        {
            var deleted = 0;
            foreach (var row in rows)
            {
                if (pool.Contains(accountOf(row))) continue;
                _unitOfWork.Repository<T>().Delete(row);
                deleted++;
            }
            return deleted;
        }

        private async Task<int> CommitIfAnyAsync(int deleted, string resourceType, Guid resourceId)
        {
            if (deleted == 0) return 0;
            await _unitOfWork.CommitAsync();
            _logger.LogInformation(
                "Đã dọn {Count} override tài khoản mồ côi ({ResourceType} {ResourceId}).",
                deleted, resourceType, resourceId);
            return deleted;
        }
    }
}
