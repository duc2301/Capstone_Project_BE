using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IFolderPermissionService
    {
        Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetGroupFolderPermissionResponsesAsync(Guid folderId);

        Task<FolderPermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid folderId, Guid callerAccountId);

        /// <summary>
        /// Roster for the "Phân quyền thành viên" dialog on a folder: every member who has access via
        /// a group, with their inherited level and whether they are blacklisted on this folder.
        /// </summary>
        Task<MemberPermissionsViewModelDTO> GetMemberPermissionsAsync(Guid folderId, Guid callerAccountId);

        /// <summary>
        /// Bulk blacklist/un-blacklist members on a folder (the "add-user" save). A blacklist persists
        /// as an active blocking (deny) override row that applies to the whole subtree; un-blacklisted
        /// accounts fall back to inheriting the group ACL.
        /// </summary>
        Task<IEnumerable<UserPermissionResponseDTO>> BulkUpdateFolderUserPermissionsAsync(AddUserPermissionsBulkDTO dto, Guid actorId);

        Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetActiveParticipantsByFolderId(Guid folderId);

        Task<IEnumerable<GroupFolderPermissionResponseDTO>> BulkUpdateFolderPermissionsAsync(AddPermissionsBulkDTO dto, Guid actorId);

        Task<GroupFolderPermissionResponseDTO> GetFolderPermissionOfParticipantByFolderIdAndParticipantId(GetFolderPermissionOfParticipantDTO dto);
    }
}
