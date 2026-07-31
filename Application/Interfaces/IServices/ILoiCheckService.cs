using Application.DTOs.ResponseDTOs.Loi;
using Domain.Enum.Loi;

namespace Application.Interfaces.IServices
{
    public interface ILoiCheckService
    {
        Task<LoiCheckResponseDTO?> GetByFileItemAsync(
            Guid fileItemId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiCheckResponseDTO> RecomputeAsync(
            Guid fileItemId, LoiStage targetStage, Guid actor, bool isSystemAdmin, CancellationToken ct = default);
    }
}
