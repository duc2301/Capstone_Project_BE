using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IRepositories
{
    public interface IFolderPermissionRepository
    {
        Task<IEnumerable<FolderPermission>> GetPartipatedGroupFolderPermissionsByFolderIdAsync(Guid folderId);
        Task<Dictionary<Guid, FolderPermission>> GetActivePartipantsByFolderIdAsync(Guid folderId);
        Task<IEnumerable<FolderPermission>> GetActiveGroupsByFolderItemId(Guid folderId);
        Task<Dictionary<Guid, FolderPermission>> GetFolderPermissionsByFolderIdAsync(Guid folderId, List<Guid> participantIds);
        Task<IEnumerable<FolderPermission>> GetFolderPermissionsByParticipantIdsAsync(Guid folderId, List<Guid> listFolderPermissionId);
        Task<FolderPermission?> GetFolderPermissionByFolderIdAndParticipantIdAsync(Guid folderId, Guid participantId);
        Task<IEnumerable<ParticipantItems>> GetAllParticipantsByFolderIdAsync(Guid folderId);
        Task<HashSet<Guid>> GetCallerParticipantIdsByFolderIdAsync(Guid folderId, Guid accountId);

        // ===== Per-user "Phân quyền thành viên" (blacklist) UI =====

        /// <summary>
        /// Active group grants on this folder (view-granting rows), one per participant with its level
        /// and group name — the folder side of the roster resolution.
        /// </summary>
        Task<List<GroupGrantDTO>> GetActiveGroupGrantsByFolderIdAsync(Guid folderId);

        /// <summary>Active members of the given participants (group -> people), for building the roster.</summary>
        Task<List<MemberOfParticipantDTO>> GetActiveMembersByParticipantIdsAsync(List<Guid> participantIds);

        /// <summary>
        /// Active per-account override rows on this folder (AccountId-keyed), indexed by AccountId —
        /// used to flag which roster members are blacklisted.
        /// </summary>
        Task<Dictionary<Guid, FolderPermission>> GetActiveAccountOverridesByFolderIdAsync(Guid folderId);

        /// <summary>Existing per-account override rows for the given accounts (tracked, for bulk upsert).</summary>
        Task<Dictionary<Guid, FolderPermission>> GetAccountOverridesByFolderIdAsync(Guid folderId, List<Guid> accountIds);

        /// <summary>
        /// Per-account override rows for the given accounts, any status, with the Account included
        /// (no tracking) — used to build the bulk-save response after commit.
        /// </summary>
        Task<List<FolderPermission>> GetAccountOverrideRowsByFolderIdAsync(Guid folderId, List<Guid> accountIds);
    }
}
