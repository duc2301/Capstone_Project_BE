using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.RequestDTOs.ZoneReturn;

namespace Application.Interfaces.IServices
{
    public interface IZoneReturnRequestService
    {
        Task<ApiResponse> CreateAsync(Guid fileItemId, CreateZoneReturnRequestDTO dto, Guid actorId);
        // projectId null = mọi dự án actor có vai trò Leader; có giá trị = chỉ dự án đó (mở từ trong 1
        // dự án cụ thể -> không gộp lẫn request của dự án khác actor cũng lãnh đạo nhóm).
        Task<ApiResponse> GetPendingAsync(Guid actorId, Guid? projectId = null);
        Task<ApiResponse> ApproveAsync(Guid requestId, Guid actorId);
        Task<ApiResponse> RejectAsync(Guid requestId, RejectZoneReturnRequestDTO dto, Guid actorId);
    }
}
