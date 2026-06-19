using Application.DTOs.RequestDTOs.Profile;
using Application.DTOs.ResponseDTOs.Profile;

namespace Application.Interfaces.IServices
{
    // Profile = view/edit account của chính user hiện tại. AccountId do controller lấy từ JWT truyền vào.
    public interface IProfileService
    {
        Task<ProfileResponseDTO> GetMyProfileAsync(Guid accountId);
        Task<ProfileResponseDTO> UpdateMyProfileAsync(Guid accountId, UpdateProfileDTO dto);
        Task ChangePasswordAsync(Guid accountId, ChangePasswordDTO dto);
    }
}
