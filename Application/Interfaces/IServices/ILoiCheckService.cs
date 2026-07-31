using Application.DTOs.ResponseDTOs.Loi;

namespace Application.Interfaces.IServices
{
    public interface ILoiCheckService
    {
        Task<LoiCheckResponseDTO?> GetByFileItemAsync(Guid fileItemId, CancellationToken ct = default);

        Task<LoiCheckResponseDTO> RecomputeAsync(Guid fileItemId, CancellationToken ct = default);
    }
}
