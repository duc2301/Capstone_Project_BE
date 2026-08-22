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

        // ===== Per-user "Phân quyền thành viên" (blacklist) UI =====

        /// <summary>
        /// Active group grants on this FILE (present-wins overrides, incl. denies), one row per
        /// participant with its level and group name — the file side of the roster resolution.
        /// </summary>
        Task<List<GroupGrantDTO>> GetActiveGroupGrantsByFileItemIdAsync(Guid fileItemId);

        /// <summary>Active members of the given participants (group -> people), for building the roster.</summary>
        Task<List<MemberOfParticipantDTO>> GetActiveMembersByParticipantIdsAsync(List<Guid> participantIds);

        /// <summary>
        /// Active per-account override rows on this file (AccountId-keyed), indexed by AccountId —
        /// used to flag which roster members are blacklisted.
        /// </summary>
        Task<Dictionary<Guid, FilePermission>> GetActiveAccountOverridesByFileItemIdAsync(Guid fileItemId);

        /// <summary>Existing per-account override rows for the given accounts (tracked, for bulk upsert).</summary>
        Task<Dictionary<Guid, FilePermission>> GetAccountOverridesByFileItemIdAsync(Guid fileItemId, List<Guid> accountIds);

        /// <summary>
        /// Per-account override rows for the given accounts, any status, with the Account included
        /// (no tracking) — used to build the bulk-save response after commit.
        /// </summary>
        Task<List<FilePermission>> GetAccountOverrideRowsByFileItemIdAsync(Guid fileItemId, List<Guid> accountIds);
    }
}
