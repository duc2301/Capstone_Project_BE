using Application.DTOs.ResponseDTOs.PermissionChecking;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Domain.Entities;

namespace Application.Services
{
    /// <summary>
    /// Centralized permission evaluation. Baseline flow only:
    /// look up the user's permission record and check the requested flag —
    /// no inheritance, no PM/Admin bypass, no caching yet.
    /// </summary>
    public class PermissionCheckingService : IPermissionCheckingService
    {
        private readonly IPermissionCheckingRepository _permissionCheckingRepository;

        public PermissionCheckingService(IPermissionCheckingRepository permissionCheckingRepository)
        {
            _permissionCheckingRepository = permissionCheckingRepository;
        }

        // ===== Folder permissions =====

        public Task CanViewFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanView, "View");

        public Task CanEditFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanEdit, "Edit");

        public Task CanUpdateFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanUpdate, "Update");

        public Task CanDownloadFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanDownload, "Download");

        public Task CanVerifyFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanVerify, "Verify");

        public Task CanApproveFolderAsync(Guid folderId, Guid accountId)
            => CheckFolderAsync(folderId, accountId, fp => fp.CanApprove, "Approve");

        // ===== File permissions =====

        public Task CanViewFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanView, "View");

        public Task CanEditFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanEdit, "Edit");

        public Task CanUpdateFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanUpdate, "Update");

        public Task CanDownloadFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanDownload, "Download");

        public Task CanVerifyFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanVerify, "Verify");

        public Task CanApproveFileAsync(Guid fileItemId, Guid accountId)
            => CheckFileAsync(fileItemId, accountId, fp => fp.CanApprove, "Approve");

        // ===== Current-user permission retrieval (viewing only) =====

        public async Task<CurrentUserPermissionsResponseDTO> GetCurrentUserPermissionsAsync(Guid accountId)
        {
            var account = await GetAccountOrThrowAsync(accountId);

            // account -> active group memberships -> active participants -> all permission records
            var memberships = await _permissionCheckingRepository.GetActiveGroupMembershipsAsync(accountId);
            var groupIds = memberships.Select(m => m.GroupId).Distinct().ToList();

            var participants = await _permissionCheckingRepository.GetActiveParticipantsByGroupIdsAsync(groupIds);
            var participantIds = participants.Select(pp => pp.Id).ToList();

            var folderPermissions = await _permissionCheckingRepository.GetFolderPermissionsByParticipantIdsAsync(participantIds);
            var filePermissions = await _permissionCheckingRepository.GetFilePermissionsByParticipantIdsAsync(participantIds);

            return new CurrentUserPermissionsResponseDTO
            {
                CurrentUser = BuildCurrentUser(account),
                Groups = memberships
                    .Select(m => m.Group)
                    .DistinctBy(g => g.Id)
                    .Select(g => new CurrentUserGroupDTO
                    {
                        GroupId = g.Id,
                        Name = g.Name,
                        OrganizationId = g.OrganizationId
                    })
                    .ToList(),
                ProjectParticipants = participants
                    .Select(pp => new CurrentUserParticipantDTO
                    {
                        ProjectParticipantId = pp.Id,
                        ProjectId = pp.ProjectId,
                        ProjectName = pp.Project.ProjectName,
                        GroupId = pp.GroupId,
                        Role = pp.Role,
                        Status = pp.Status
                    })
                    .ToList(),
                Permissions = new CurrentUserPermissionListDTO
                {
                    FolderPermissions = folderPermissions
                        .Select(fp => BuildFolderPermissionItem(fp, fp.Folder.Name))
                        .ToList(),
                    FilePermissions = filePermissions
                        .Select(fp => BuildFilePermissionItem(fp, fp.FileItem.Name))
                        .ToList()
                }
            };
        }

        public async Task<CurrentUserFolderPermissionResponseDTO> GetCurrentUserFolderPermissionAsync(Guid folderId, Guid accountId)
        {
            var account = await GetAccountOrThrowAsync(accountId);

            var folder = await _permissionCheckingRepository.GetFolderAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            var permission = await _permissionCheckingRepository.GetUserFolderPermissionAsync(folderId, accountId);

            return new CurrentUserFolderPermissionResponseDTO
            {
                CurrentUser = BuildCurrentUser(account),
                FolderId = folder.Id,
                FolderName = folder.Name,
                Permission = permission == null ? null : BuildFolderPermissionItem(permission, folder.Name)
            };
        }

        public async Task<CurrentUserFilePermissionResponseDTO> GetCurrentUserFilePermissionAsync(Guid fileItemId, Guid accountId)
        {
            var account = await GetAccountOrThrowAsync(accountId);

            var fileItem = await _permissionCheckingRepository.GetFileItemAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            var permission = await _permissionCheckingRepository.GetUserFilePermissionAsync(fileItemId, accountId);

            return new CurrentUserFilePermissionResponseDTO
            {
                CurrentUser = BuildCurrentUser(account),
                FileItemId = fileItem.Id,
                FileName = fileItem.Name,
                Permission = permission == null ? null : BuildFilePermissionItem(permission, fileItem.Name)
            };
        }

        private async Task<Account> GetAccountOrThrowAsync(Guid accountId)
        {
            return await _permissionCheckingRepository.GetAccountAsync(accountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);
        }

        private static CurrentUserDTO BuildCurrentUser(Account account) => new()
        {
            AccountId = account.Id,
            UserName = account.UserName,
            Email = account.Email
        };

        private static CurrentUserFolderPermissionItemDTO BuildFolderPermissionItem(
            FolderPermission fp, string folderName) => new()
        {
            PermissionId = fp.Id,
            FolderId = fp.FolderId,
            FolderName = folderName,
            ProjectParticipantId = fp.ProjectParticipantId,
            CanView = fp.CanView,
            CanEdit = fp.CanEdit,
            CanUpdate = fp.CanUpdate,
            CanDownload = fp.CanDownload,
            CanVerify = fp.CanVerify,
            CanApprove = fp.CanApprove,
            Status = fp.Status
        };

        private static CurrentUserFilePermissionItemDTO BuildFilePermissionItem(
            FilePermission fp, string fileName) => new()
        {
            PermissionId = fp.Id,
            FileItemId = fp.FileItemId,
            FileName = fileName,
            ProjectParticipantId = fp.ProjectParticipantId,
            CanView = fp.CanView,
            CanEdit = fp.CanEdit,
            CanUpdate = fp.CanUpdate,
            CanDownload = fp.CanDownload,
            CanVerify = fp.CanVerify,
            CanApprove = fp.CanApprove,
            Status = fp.Status
        };

        // ===== Shared evaluation =====

        private async Task CheckFolderAsync(
            Guid folderId, Guid accountId, Func<FolderPermission, bool> hasPermission, string action)
        {
            var permission = await _permissionCheckingRepository
                .GetUserFolderPermissionAsync(folderId, accountId);

            if (permission == null || !hasPermission(permission))
                throw new ApiExceptionResponse(
                    $"You do not have '{action}' permission on this folder.", 403);
        }

        private async Task CheckFileAsync(
            Guid fileItemId, Guid accountId, Func<FilePermission, bool> hasPermission, string action)
        {
            var permission = await _permissionCheckingRepository
                .GetUserFilePermissionAsync(fileItemId, accountId);

            if (permission == null || !hasPermission(permission))
                throw new ApiExceptionResponse(
                    $"You do not have '{action}' permission on this file.", 403);
        }
    }
}
