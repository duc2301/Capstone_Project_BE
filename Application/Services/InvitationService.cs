using System.Security.Cryptography;
using Application.DTOs.RequestDTOs.Invitation;
using Application.DTOs.ResponseDTOs.Invitation;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Invitation;
using Domain.Enum.Project;

namespace Application.Services
{
    // Luồng "tạo nhóm và mời thành viên" trong sơ đồ.
    // Người thực hiện (Manager mời / Member accept) lấy từ JWT qua ICurrentUserService — không nhận trong body.
    public class InvitationService : IInvitationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notification;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        public InvitationService(
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

        public async Task<InvitationResponseDTO> InviteAsync(InviteRequestDTO dto)
        {
            if (dto.InvitedAccountId == null && dto.InvitedGroupId == null)
                throw new ApiExceptionResponse("Must provide InvitedAccountId or InvitedGroupId.", 400);

            var inviter = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(dto.ProjectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var invitation = _mapper.Map<ProjectInvitation>(dto);
            invitation.Id = Guid.NewGuid();
            invitation.InvitedByAccountId = inviter;
            invitation.Token = GenerateToken();
            invitation.Status = InvitationStatus.Pending;
            invitation.CreatedAt = DateTime.UtcNow;
            invitation.ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpireDays > 0 ? dto.ExpireDays : 7);

            await _unitOfWork.Repository<ProjectInvitation>().CreateAsync(invitation);
            await _unitOfWork.CommitAsync();

            if (dto.InvitedAccountId.HasValue)
            {
                await _notification.NotifyAsync(
                    dto.InvitedAccountId.Value,
                    $"Bạn được mời tham gia dự án {project.ProjectName}",
                    senderName: _currentUser.UserName ?? "System",
                    linkType: "ProjectInvitation",
                    linkId: invitation.Id.ToString());
            }

            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        public async Task<InvitationResponseDTO> AcceptAsync(string token)
        {
            var accountId = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var invitation = await FindActiveByTokenAsync(token);

            if (invitation.InvitedAccountId.HasValue && invitation.InvitedAccountId.Value != accountId)
                throw new ApiExceptionResponse("This invitation is for a different account.", 403);

            await _unitOfWork.Repository<ProjectParticipant>().CreateAsync(new ProjectParticipant
            {
                Id = Guid.NewGuid(),
                ProjectId = invitation.ProjectId,
                OrganizationId = null,
                GroupId = invitation.InvitedGroupId,
                Role = ProjectParticipantRole.Member,
                JoinedAt = DateTime.UtcNow
            });

            invitation.Status = InvitationStatus.Accepted;
            invitation.RespondedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();
            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        public async Task<InvitationResponseDTO> RejectAsync(string token)
        {
            var accountId = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var invitation = await FindActiveByTokenAsync(token);

            if (invitation.InvitedAccountId.HasValue && invitation.InvitedAccountId.Value != accountId)
                throw new ApiExceptionResponse("This invitation is for a different account.", 403);

            invitation.Status = InvitationStatus.Rejected;
            invitation.RespondedAt = DateTime.UtcNow;

            await _unitOfWork.CommitAsync();
            return _mapper.Map<InvitationResponseDTO>(invitation);
        }

        // Generic repo chưa hỗ trợ predicate -> tìm bằng GetAllAsync + LINQ (dataset nhỏ, chấp nhận).
        private async Task<ProjectInvitation> FindActiveByTokenAsync(string token)
        {
            var all = await _unitOfWork.Repository<ProjectInvitation>().GetAllAsync();
            var invitation = all.FirstOrDefault(i => i.Token == token)
                ?? throw new ApiExceptionResponse("Invitation not found.", 404);

            if (invitation.Status != InvitationStatus.Pending)
                throw new ApiExceptionResponse($"Invitation already {invitation.Status}.", 409);

            if (invitation.ExpiresAt < DateTime.UtcNow)
            {
                invitation.Status = InvitationStatus.Expired;
                await _unitOfWork.CommitAsync();
                throw new ApiExceptionResponse("Invitation expired.", 410);
            }

            return invitation;
        }

        private static string GenerateToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
