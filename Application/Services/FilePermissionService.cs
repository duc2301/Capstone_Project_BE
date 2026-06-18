using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Account;
using Application.DTOs.ResponseDTOs.Permission;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Permission;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class FilePermissionService : IFilePermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FilePermissionService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        #region CRUD có sẵn
        public Task<FilePermissionResponseDTO> CreateAsync(CreateFilePermissionDTO dto)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<FilePermissionResponseDTO>> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task<FilePermissionResponseDTO?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }


        public Task<FilePermissionResponseDTO> UpdateAsync(Guid id, UpdateFilePermissionDTO dto)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Lấy data cho frontend

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetPartipatedGroupFilePermissionsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items);
        }

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> UpdatePermissionBulk(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetPartipatedGroupFilePermissionsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items);
        }

        #endregion

        #region Create/Update permissions theo bulk

        public async Task<IEnumerable<FilePermissionResponseDTO>> BulkUpdateFilePermissionsAsync(AddPermissionsBulkDTO dto)
        {
            //if (!dto.GroupsPermission.Any()) 
            //    throw new ApiExceptionResponse("GroupsPermission list is empty.", 400);

            var participantIds = dto.GroupsPermission.Select(u => u.ProjectParticipantId).Union(dto.RemoveParticipantIds).ToList();

            // Get all existing permissions in one query
            var existingPermissions = await _unitOfWork.FilePermissionRepository.GetFilePermissionsByFileItemIdAsync(dto.FileItemId, participantIds);

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
            var toCreate = new List<FilePermission>();


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
                    permission = new FilePermission
                    {
                        Id = Guid.NewGuid(),
                        FileItemId = dto.FileItemId,
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
                await _unitOfWork.Repository<FilePermission>().CreateRangeAsync(toCreate);

            await _unitOfWork.CommitAsync();

            var permissions = await _unitOfWork.FilePermissionRepository.GetFilePermissionsByParticipantIdsAsync(dto.FileItemId, updatedParticipantIds);

            return _mapper.Map<IEnumerable<FilePermissionResponseDTO>>(permissions);
        }

        #endregion
    }
}
