using Application.DTOs.RequestDTOs.Invitation;
using Application.DTOs.ResponseDTOs.Invitation;

namespace Application.Interfaces.IServices
{
    public interface IInvitationService
    {
        // AccountId của người thao tác lấy từ JWT (ICurrentUserService)
        Task<InvitationResponseDTO> InviteAsync(InviteRequestDTO dto);

        // Người được mời tự accept/reject — by InvitationId, JWT đã chứng minh danh tính.
        Task<InvitationResponseDTO> AcceptAsync(Guid invitationId);
        Task<InvitationResponseDTO> RejectAsync(Guid invitationId);

        // Danh sách lời mời Pending của user hiện tại — UI "Lời mời của tôi".
        Task<IEnumerable<MyInvitationDTO>> GetMyPendingAsync();
    }
}
