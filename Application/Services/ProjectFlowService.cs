using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Account;

namespace Application.Services
{
    public class ProjectFlowService : IProjectFlowService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notification;
        private readonly ICurrentUserService _currentUser;
        private readonly IFolderBootstrapService _folderBootstrap;
        private readonly IMapper _mapper;

        public ProjectFlowService(
            IUnitOfWork unitOfWork,
            INotificationService notification,
            ICurrentUserService currentUser,
            IFolderBootstrapService folderBootstrap,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _notification = notification;
            _currentUser = currentUser;
            _folderBootstrap = folderBootstrap;
            _mapper = mapper;
        }

        // Admin gán 1 account hiện có làm PM của project.
        // 1 account có thể làm PM nhiều dự án -> chỉ validate account tồn tại + active.
        public async Task<ProjectResponseDTO> AssignManagerAsync(
            Guid projectId, AssignProjectManagerDTO dto)
        {
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var account = await _unitOfWork.AccountRepository.GetByIdAsync(dto.AccountId)
                ?? throw new ApiExceptionResponse("Account not found.", 404);

            if (account.Status == AccountStatus.Suspended || account.Status == AccountStatus.Inactive)
                throw new ApiExceptionResponse("Cannot assign an inactive account as manager.", 409);

            var oldManagerId = project.ManagerAccountId;
            project.ManagerAccountId = account.Id;

            await _unitOfWork.CommitAsync();

            await _notification.NotifyAsync(
                account.Id,
                $"Bạn được chỉ định làm Project Manager cho dự án {project.ProjectName}",
                senderName: _currentUser.UserName ?? "Admin",
                linkType: "Project",
                linkId: project.Id.ToString());

            if (oldManagerId.HasValue && oldManagerId.Value != account.Id)
            {
                await _notification.NotifyAsync(
                    oldManagerId.Value,
                    $"Bạn đã được thay khỏi vai trò Project Manager của dự án {project.ProjectName}",
                    senderName: _currentUser.UserName ?? "Admin",
                    linkType: "Project",
                    linkId: project.Id.ToString());
            }

            return _mapper.Map<ProjectResponseDTO>(project);
        }

        // PM gọi: add nhiều Group vô project trong 1 transaction.
        // (Org info suy ra qua Group.OrganizationId — không nhận trực tiếp Organization)
        public async Task<List<ParticipantResponseDTO>> AddParticipantsAsync(
            Guid projectId, AddParticipantsBulkDTO dto)
        {
            if (dto.Participants == null || dto.Participants.Count == 0)
                throw new ApiExceptionResponse("Participants list is empty.", 400);

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);
            if (_currentUser.SystemRole != AccountRole.Admin.ToString()
                && project.ManagerAccountId != actor)
                throw new ApiExceptionResponse("Only the project manager or Admin can add participants.", 403);

            var now = DateTime.UtcNow;
            var created = new List<ProjectParticipant>(dto.Participants.Count);

            for (int i = 0; i < dto.Participants.Count; i++)
            {
                var p = dto.Participants[i];
                if (p.GroupId == Guid.Empty)
                    throw new ApiExceptionResponse($"Participants[{i}]: GroupId is required.", 400);

                var entity = new ProjectParticipant
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    GroupId = p.GroupId,
                    Role = p.Role,
                    JoinedAt = now
                };
                await _unitOfWork.Repository<ProjectParticipant>().CreateAsync(entity);
                created.Add(entity);
            }

            await _unitOfWork.CommitAsync();

            // Tạo "ô" thư mục CDE cho từng bên vừa thêm (WIP/Shared/Published/Archived).
            foreach (var groupId in created.Select(c => c.GroupId).Distinct())
                await _folderBootstrap.ScaffoldParticipantFoldersAsync(projectId, groupId);

            return created.Select(_mapper.Map<ParticipantResponseDTO>).ToList();
        }

        public async Task<List<ParticipantResponseDTO>> GetParticipantsAsync(Guid projectId)
        {
            _ = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var participants = (await _unitOfWork.Repository<ProjectParticipant>().GetAllAsync())
                .Where(p => p.ProjectId == projectId)
                .ToList();

            return participants.Select(_mapper.Map<ParticipantResponseDTO>).ToList();
        }
    }
}
