using Application.DTOs.RequestDTOs.Loi;
using Application.DTOs.ResponseDTOs.Loi;
using Domain.Enum.Loi;

namespace Application.Interfaces.IServices
{
    public interface IProjectLoiRuleService
    {
        Task<LoiRuleSetDTO?> GetRuleSetSummaryAsync(Guid projectId, CancellationToken ct = default);

        Task<LoiRuleSetDTO?> GetRuleSetAsync(Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiRuleSetDTO> UpdateRuleSetAsync(
            Guid projectId, UpdateLoiRuleSetDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task DeleteRuleSetAsync(Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<byte[]> GenerateTemplateAsync(Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiImportPreviewDTO> ParseImportAsync(
            Guid projectId, Stream stream, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiRuleSetDTO> CommitImportAsync(
            Guid projectId, LoiImportCommitDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<IReadOnlyList<LoiComponentDTO>> GetComponentsAsync(
            Guid projectId, LoiDiscipline? discipline, string? search, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiComponentDTO> CreateComponentAsync(
            Guid projectId, CreateLoiComponentDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiComponentDTO> UpdateComponentAsync(
            Guid projectId, Guid componentId, UpdateLoiComponentDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task DeleteComponentAsync(
            Guid projectId, Guid componentId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiMatrixDTO> GetMatrixAsync(
            Guid projectId, Guid componentId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiMatrixDTO> SaveMatrixAsync(
            Guid projectId, Guid componentId, SaveLoiMatrixDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiMatrixDTO> RenameVariantAsync(
            Guid projectId, Guid componentId, RenameLoiVariantDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiMatrixDTO> DeleteVariantAsync(
            Guid projectId, Guid componentId, string? variant, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<IReadOnlyList<LoiParameterDTO>> GetParametersAsync(
            Guid projectId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiParameterDTO> CreateParameterAsync(
            Guid projectId, CreateLoiParameterDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task<LoiParameterDTO> UpdateParameterAsync(
            Guid projectId, Guid parameterId, UpdateLoiParameterDTO dto, Guid actor, bool isSystemAdmin, CancellationToken ct = default);

        Task DeleteParameterAsync(
            Guid projectId, Guid parameterId, Guid actor, bool isSystemAdmin, CancellationToken ct = default);
    }
}
