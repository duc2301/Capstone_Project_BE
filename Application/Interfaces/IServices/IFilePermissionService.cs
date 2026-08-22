using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFilePermissionService
    {
        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId);

        Task<IEnumerable<GroupFilePermissionResponseDTO>> BulkUpdateFilePermissionsAsync(AddPermissionsBulkDTO dto, Guid actorId);

        Task<FilePermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid fileItemId, Guid callerAccountId);

        /// <summary>
        /// Roster for the "Phân quyền thành viên" dialog on a file: every member who has access via a
        /// group, with their inherited level and whether they are blacklisted on this file.
        /// </summary>
        Task<MemberPermissionsViewModelDTO> GetMemberPermissionsAsync(Guid fileItemId, Guid callerAccountId);

        /// <summary>
        /// Bulk blacklist/un-blacklist members on a file (the "add-user" save). A blacklist persists as
        /// an active blocking (deny) override row; un-blacklisted accounts fall back to the group ACL.
        /// </summary>
        Task<IEnumerable<UserPermissionResponseDTO>> BulkUpdateFileUserPermissionsAsync(AddUserPermissionsBulkDTO dto, Guid actorId);

        Task<IEnumerable<GroupFilePermissionResponseDTO>> GetActiveParticipantsByFileItemId(Guid fileItemId);

        Task<GroupFilePermissionResponseDTO> GetFilePermissionOfParticipantByFileItemIdAndParticipantId(GetFilePermissionOfParticipantDTO dto);
    }
}