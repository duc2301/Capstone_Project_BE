using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Permission;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Permission;

namespace Application.Services
{
    public class FolderPermissionService : IFolderPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FolderPermissionService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        #region Lấy data cho frontend
        public async Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetGroupFolderPermissionResponsesAsync(Guid folderId)
        {
            var items = await _unitOfWork.FolderPermissionRepository.GetPartipatedGroupFolderPermissionsByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(items);
        }

        public async Task<FolderPermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid folderId)
        {
            var items = await _unitOfWork.FolderPermissionRepository.GetActivePartipantsByFolderIdAsync(folderId);

            var activeGroupOfFolder = _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(items.Values.ToList());

            var allProjectParticipants = await _unitOfWork.FolderPermissionRepository.GetAllParticipantsByFolderIdAsync(folderId);

            var availableGroups = allProjectParticipants.Where(pp => !items.ContainsKey(pp.ProjectParticipantId)).ToList();

            return new FolderPermissionsViewModelDTO
            {
                AvailableGroups = availableGroups,
                SelectedPermissions = activeGroupOfFolder.ToList()
            };
        }

        public async Task<IEnumerable<GroupFolderPermissionResponseDTO>> GetActiveParticipantsByFolderId(Guid folderId)
        {
            var items = await _unitOfWork.FolderPermissionRepository.GetActivePartipantsByFolderIdAsync(folderId);
            return _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(items.Values.ToList());
        }

        #endregion

        #region Create/Update permissions theo bulk

        public async Task<IEnumerable<GroupFolderPermissionResponseDTO>> BulkUpdateFolderPermissionsAsync(AddPermissionsBulkDTO dto)
        {
            //if (!dto.GroupsPermission.Any()) 
            //    throw new ApiExceptionResponse("GroupsPermission list is empty.", 400);

            var participantIds = dto.GroupsPermission.Select(u => u.ProjectParticipantId).Union(dto.RemoveParticipantIds).ToList();

            // Get all existing permissions in one query
            var existingPermissions = await _unitOfWork.FolderPermissionRepository.GetFolderPermissionsByFolderIdAsync(dto.Id, participantIds);

            var updatedParticipantIds = new List<Guid>();

            // Remove permissions for participants in the removal list
            foreach (var participantId in dto.RemoveParticipantIds)
            {
                if (existingPermissions.TryGetValue(participantId, out var perm))
                {
                    perm.Status = PermissionStatus.Inactive;
                    perm.CanView = false;
                    perm.CanEdit = false;
                    perm.CanUpdate = false;
                    perm.CanDownload = false;
                    perm.CanVerify = false;
                    perm.CanApprove = false;

                    updatedParticipantIds.Add(participantId);

                }
            }

            // Create/Update permissions for participants in the update list
            var toCreate = new List<FolderPermission>();


            foreach (var u in dto.GroupsPermission)
            {
                if (existingPermissions.TryGetValue(u.ProjectParticipantId, out var permission))
                {
                    // Update existing rows if the group was previously assigned permissions but then removed
                    permission.CanView = u.CanView;
                    permission.CanEdit = u.CanEdit;
                    permission.CanUpdate = u.CanUpdate;
                    permission.CanDownload = u.CanDownload;
                    permission.CanVerify = u.CanVerify;
                    permission.CanApprove = u.CanApprove;
                    permission.Status = PermissionStatus.Active;

                }
                else
                {
                    permission = new FolderPermission
                    {
                        Id = Guid.NewGuid(),
                        FolderId = dto.Id,
                        ProjectParticipantId = u.ProjectParticipantId
                    };

                    toCreate.Add(permission);
                }

                permission.CanView = u.CanView;
                permission.CanEdit = u.CanEdit;
                permission.CanUpdate = u.CanUpdate;
                permission.CanDownload = u.CanDownload;
                permission.CanVerify = u.CanVerify;
                permission.CanApprove = u.CanApprove;
                permission.Status = PermissionStatus.Active;

                updatedParticipantIds.Add(u.ProjectParticipantId);

            }

            if (toCreate.Any())
                await _unitOfWork.Repository<FolderPermission>().CreateRangeAsync(toCreate);

            await _unitOfWork.CommitAsync();

            var permissions = await _unitOfWork.FolderPermissionRepository.GetFolderPermissionsByParticipantIdsAsync(dto.Id, updatedParticipantIds);

            return _mapper.Map<IEnumerable<GroupFolderPermissionResponseDTO>>(permissions);
        }

        #endregion


        #region Check quyền

        public async Task<GroupFolderPermissionResponseDTO> GetFolderPermissionOfParticipantByFolderIdAndParticipantId(GetFolderPermissionOfParticipantDTO dto)
        {
            var permission = await _unitOfWork.FolderPermissionRepository.GetFolderPermissionByFolderIdAndParticipantIdAsync(dto.FolderId, dto.ParticipantId);
            if (permission == null)
                throw new ApiExceptionResponse("No permission found for the specified participant and folder item.", 404);
            return _mapper.Map<GroupFolderPermissionResponseDTO>(permission);
        }



        #endregion
    }
}
