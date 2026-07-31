using Application.DTOs.RequestDTOs.Loi;
using Application.DTOs.ResponseDTOs.Loi;

namespace Application.Interfaces.IServices
{
    public interface ILoiAliasService
    {
        Task<IReadOnlyList<LoiAliasResponseDTO>> GetByProjectAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiAliasResponseDTO> CreateAsync(
            Guid projectId, CreateLoiAliasDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task DeleteAsync(
            Guid projectId, Guid aliasId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);
    }
}
