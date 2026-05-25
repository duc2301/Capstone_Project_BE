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
        private readonly IMapper _mapper;

        public ProjectFlowService(
            IUnitOfWork unitOfWork,
            INotificationService notification,
            ICurrentUserService currentUser,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _notification = notification;
            _currentUser = currentUser;
            _mapper = mapper;
        }

        // Admin gọi: tạo account làm PM cho project trống + gán Project.ManagerAccountId
        public async Task<ProjectManagerCreatedResponseDTO> CreateManagerAsync(
            Guid projectId, CreateProjectManagerDTO dto)
        {
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            if (project.ManagerAccountId.HasValue)
                throw new ApiExceptionResponse("Project already has a manager. Replace via PUT /api/projects/{id}.", 409);

            if (await _unitOfWork.AccountRepository.EmailExistsAsync(dto.Email))
                throw new ApiExceptionResponse("Email already exists.", 409);

            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserName = dto.UserName,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = AccountRole.User,            // system role: User (Manager là per-project)
                Status = AccountStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.AccountRepository.CreateAsync(account);

            project.ManagerAccountId = account.Id;
            // Project được EF track sẵn -> mutate là đủ, không cần repo.Update

            await _unitOfWork.CommitAsync();

            await _notification.NotifyAsync(
                account.Id,
                $"Bạn được chỉ định làm Project Manager cho dự án {project.ProjectName}",
                senderName: _currentUser.UserName ?? "Admin",
                linkType: "Project",
                linkId: project.Id.ToString());

            return new ProjectManagerCreatedResponseDTO
            {
                ProjectId = project.Id,
                ManagerAccountId = account.Id,
                UserName = account.UserName,
                Email = account.Email
            };
        }

        // PM gọi: thêm bên tham gia (Organization và/hoặc Group) vô project
        public async Task<ParticipantResponseDTO> AddParticipantAsync(
            Guid projectId, AddParticipantDTO dto)
        {
            if (dto.OrganizationId == null && dto.GroupId == null)
                throw new ApiExceptionResponse("Must provide OrganizationId or GroupId.", 400);

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            // Chỉ PM của project (hoặc Admin) mới được add bên tham gia
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);
            if (_currentUser.SystemRole != AccountRole.Admin.ToString()
                && project.ManagerAccountId != actor)
                throw new ApiExceptionResponse("Only the project manager or Admin can add participants.", 403);

            var participant = new ProjectParticipant
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                OrganizationId = dto.OrganizationId,
                GroupId = dto.GroupId,
                Role = dto.Role,
                JoinedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<ProjectParticipant>().CreateAsync(participant);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<ParticipantResponseDTO>(participant);
        }
    }
}
