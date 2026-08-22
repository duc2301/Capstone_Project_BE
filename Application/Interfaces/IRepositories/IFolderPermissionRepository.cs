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

        // ===== Per-account override UI (Google-Drive style) =====

        /// <summary>
        /// Distinct accounts that currently have VIEW access to the folder through their group ACL —
        /// the population shown in the per-user "Phân quyền" dialog.
        /// </summary>
        Task<List<AccountItem>> GetAudienceAccountsByFolderIdAsync(Guid folderId);

        /// <summary>
        /// Active per-account override rows on this folder (AccountId-keyed), with the Account
        /// included, indexed by AccountId — the "selected users" side of the dialog.
        /// </summary>
        Task<Dictionary<Guid, FolderPermission>> GetActiveAccountOverridesByFolderIdAsync(Guid folderId);

        /// <summary>Existing per-account override rows for the given accounts (tracked, for bulk upsert).</summary>
        Task<Dictionary<Guid, FolderPermission>> GetAccountOverridesByFolderIdAsync(Guid folderId, List<Guid> accountIds);
    }
}
