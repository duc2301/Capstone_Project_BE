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
    public class FilePermissionService : IFilePermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        private readonly IAuditLogService _auditLog;

        public FilePermissionService(IUnitOfWork unitOfWork, IMapper mapper, IAuditLogService auditLog)
        {
            _auditLog = auditLog;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        #region CRUD có sẵn
        //bo
        #endregion

        #region Lấy data cho frontend

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetPartipatedGroupFilePermissionsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items);
        }

        public async Task<FilePermissionsViewModelDTO> GetDataForPermissionUIAsync(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetActivePartipantsByFileItemIdAsync(fileItemId);

            var activeGroupOfFile = _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items.Values.ToList());

            var allProjectParticipants = await _unitOfWork.FilePermissionRepository.GetAllParticipantsByFileItemIdAsync(fileItemId);

            var availableGroups = allProjectParticipants.Where(pp => !items.ContainsKey(pp.ProjectParticipantId)).ToList();

            return new FilePermissionsViewModelDTO
            {
                AvailableGroups = availableGroups,
                SelectedPermissions = activeGroupOfFile.ToList()
            };
        }

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> GetActiveParticipantsByFileItemId(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetActivePartipantsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items.Values.ToList());
        }

        #endregion

        #region Create/Update permissions theo bulk

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> BulkUpdateFilePermissionsAsync(AddPermissionsBulkDTO dto, Guid actorId)
        {
            //if (!dto.GroupsPermission.Any()) 
            //    throw new ApiExceptionResponse("GroupsPermission list is empty.", 400);

            var participantIds = dto.GroupsPermission.Select(u => u.ProjectParticipantId).Union(dto.RemoveParticipantIds).ToList();

            // Get all existing permissions in one query
            var existingPermissions = await _unitOfWork.FilePermissionRepository.GetFilePermissionsByFileItemIdAsync(dto.Id, participantIds);

            var updatedParticipantIds = new List<Guid>();

            // Remove permissions for participants in the removal list
            foreach (var participantId in dto.RemoveParticipantIds)
            {
                if (existingPermissions.TryGetValue(participantId, out var perm))
                {
                    // Gỡ khỏi danh sách override = trả file về kế thừa quyền thư mục (dòng Inactive).
                    PermissionLevelMapper.Apply(perm, PermissionLevel.Inherit, isFile: true);

                    updatedParticipantIds.Add(participantId);

                }
            }

            // Create/Update permissions for participants in the update list
            var toCreate = new List<FilePermission>();


            foreach (var u in dto.GroupsPermission)
            {
                if (!existingPermissions.TryGetValue(u.ProjectParticipantId, out var permission))
                {
                    permission = new FilePermission
                    {
                        Id = Guid.NewGuid(),
                        FileItemId = dto.Id,
                        ProjectParticipantId = u.ProjectParticipantId
                    };

                    toCreate.Add(permission);
                }

                // Ánh xạ mức quyền qua nguồn chân lý duy nhất (đồng nhất với ma trận phân quyền).
                PermissionLevelMapper.Apply(
                    permission, PermissionLevelMapper.FromFlags(u.CanView, u.CanEdit), isFile: true);

                updatedParticipantIds.Add(u.ProjectParticipantId);

            }

            if (toCreate.Any())
                await _unitOfWork.Repository<FilePermission>().CreateRangeAsync(toCreate);

            var auditFile = await _unitOfWork.Repository<FileItem>().GetByIdAsync(dto.Id);
            var auditFolder = auditFile == null
                ? null
                : await _unitOfWork.Repository<Folder>().GetByIdAsync(auditFile.FolderId);
            await _auditLog.LogAsync(
                Domain.Enum.Audit.LogScope.Project, Domain.Enum.Audit.AuditAction.PermissionChange,
                nameof(FileItem), dto.Id.ToString(), actorId,
                detail: $"Cập nhật phân quyền tệp '{auditFile?.Name}' cho {updatedParticipantIds.Count()} bên tham gia",
                projectId: auditFolder?.ProjectId, folderId: auditFile?.FolderId);

            await _unitOfWork.CommitAsync();

            var permissions = await _unitOfWork.FilePermissionRepository.GetFilePermissionsByParticipantIdsAsync(dto.Id, updatedParticipantIds);

            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(permissions);
        }

        #endregion

        #region Check quyền

        public async Task<GroupFilePermissionResponseDTO> GetFilePermissionOfParticipantByFileItemIdAndParticipantId(GetFilePermissionOfParticipantDTO dto)
        {
            var permission = await _unitOfWork.FilePermissionRepository.GetFilePermissionByFileItemIdAndParticipantIdAsync(dto.FileItemId, dto.ParticipantId);
            if (permission == null)
                throw new ApiExceptionResponse("No permission found for the specified participant and file item.", 404);
            return _mapper.Map<GroupFilePermissionResponseDTO>(permission);
        }



        #endregion
    }
}
