using Application.DTOs.RequestDTOs.Invitation;
using Application.DTOs.ResponseDTOs.Invitation;

namespace Application.Interfaces.IServices
{
    public interface IInvitationService
    {
        // AccountId của người thao tác lấy từ JWT (ICurrentUserService)
        Task<InvitationResponseDTO> InviteAsync(InviteRequestDTO dto);
        Task<InvitationResponseDTO> AcceptAsync(string token);
        Task<InvitationResponseDTO> RejectAsync(string token);
    }
}
