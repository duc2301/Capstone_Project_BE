using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IRepositories
{
    public interface IFilePermissionRepository
    {
        Task<IEnumerable<FilePermission>> GetPartipatedGroupFilePermissionsByFileItemIdAsync(Guid fileItemId);
        Task<Dictionary<Guid, FilePermission>> GetFilePermissionsByFileItemIdAsync(Guid fileItemId, List<Guid> participantIds);
        Task<IEnumerable<FilePermission>> GetFilePermissionsByParticipantIdsAsync(Guid fileItemId, List<Guid> listFilePermissionId);
        Task<Dictionary<Guid, FilePermission>> GetActivePartipantsByFileItemIdAsync(Guid fileItemId);
        Task<IEnumerable<ParticipantItems>> GetAllParticipantsByFileItemIdAsync(Guid fileItemId);
        Task<HashSet<Guid>> GetCallerParticipantIdsByFileItemIdAsync(Guid fileItemId, Guid accountId);
        Task<IEnumerable<FilePermission>> GetActiveGroupsByFileItemId(Guid fileitemId);
        Task<FilePermission?> GetFilePermissionByFileItemIdAndParticipantIdAsync(Guid fileItemId, Guid participantId);

        // ===== Per-account override UI (Google-Drive style) =====

        /// <summary>
        /// Distinct accounts that currently have VIEW access to the file through their group ACL —
        /// the population shown in the per-user "Phân quyền" dialog. Resolves the file's group
        /// overrides first (present wins), falling back to the owning folder's group ACL, mirroring
        /// the eval-time fallback.
        /// </summary>
        Task<List<AccountItem>> GetAudienceAccountsByFileItemIdAsync(Guid fileItemId);

        /// <summary>
        /// Active per-account override rows on this file (AccountId-keyed), with the Account included,
        /// indexed by AccountId — the "selected users" side of the dialog.
        /// </summary>
        Task<Dictionary<Guid, FilePermission>> GetActiveAccountOverridesByFileItemIdAsync(Guid fileItemId);

        /// <summary>Existing per-account override rows for the given accounts (tracked, for bulk upsert).</summary>
        Task<Dictionary<Guid, FilePermission>> GetAccountOverridesByFileItemIdAsync(Guid fileItemId, List<Guid> accountIds);
    }
}
