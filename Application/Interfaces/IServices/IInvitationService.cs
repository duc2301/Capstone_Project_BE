using Application.DTOs.RequestDTOs.Invitation;
using Application.DTOs.ResponseDTOs.Invitation;

namespace Application.Interfaces.IServices
{
    public interface IInvitationService
    {
        // inviterId/inviterName của người mời do controller lấy từ JWT truyền vào.
        Task<InvitationResponseDTO> InviteAsync(InviteRequestDTO dto, Guid inviterId, string? inviterName);

        // Người được mời tự accept/reject — accountId/actorName lấy từ JWT.
        Task<InvitationResponseDTO> AcceptAsync(Guid invitationId, Guid accountId, string? actorName);
        Task<InvitationResponseDTO> RejectAsync(Guid invitationId, Guid accountId, string? actorName);

        // Danh sách lời mời Pending của user hiện tại — UI "Lời mời của tôi".
        Task<IEnumerable<MyInvitationDTO>> GetMyPendingAsync(Guid accountId);
    }
}
