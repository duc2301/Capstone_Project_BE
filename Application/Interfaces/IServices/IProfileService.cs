using Application.DTOs.RequestDTOs.Profile;
using Application.DTOs.ResponseDTOs.Profile;

namespace Application.Interfaces.IServices
{
    // Profile = view/edit account của chính user hiện tại. AccountId luôn lấy từ JWT.
    public interface IProfileService
    {
        Task<ProfileResponseDTO> GetMyProfileAsync();
        Task<ProfileResponseDTO> UpdateMyProfileAsync(UpdateProfileDTO dto);
        Task ChangePasswordAsync(ChangePasswordDTO dto);
    }
}
