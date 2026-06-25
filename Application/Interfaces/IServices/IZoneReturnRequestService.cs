using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.RequestDTOs.ZoneReturn;

namespace Application.Interfaces.IServices
{
    public interface IZoneReturnRequestService
    {
        Task<ApiResponse> CreateAsync(Guid fileItemId, CreateZoneReturnRequestDTO dto, Guid actorId);
        Task<ApiResponse> GetPendingAsync(Guid actorId);
        Task<ApiResponse> ApproveAsync(Guid requestId, Guid actorId);
        Task<ApiResponse> RejectAsync(Guid requestId, RejectZoneReturnRequestDTO dto, Guid actorId);
    }
}
