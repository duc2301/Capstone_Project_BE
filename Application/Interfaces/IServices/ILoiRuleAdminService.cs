using Application.DTOs.RequestDTOs.Loi;
using Application.DTOs.ResponseDTOs.Loi;
using Domain.Enum.Loi;

namespace Application.Interfaces.IServices
{
    public interface ILoiRuleAdminService
    {
        Task<LoiRuleSetDTO> UpdateRuleSetAsync(
            Guid ruleSetId, UpdateLoiRuleSetDTO dto, Guid actor, CancellationToken ct = default);

        Task DeleteRuleSetAsync(Guid ruleSetId, Guid actor, CancellationToken ct = default);

        Task<IReadOnlyList<LoiComponentDTO>> GetComponentsAsync(
            Guid ruleSetId, LoiDiscipline? discipline, string? search, CancellationToken ct = default);

        Task<LoiComponentDTO> CreateComponentAsync(
            Guid ruleSetId, CreateLoiComponentDTO dto, Guid actor, CancellationToken ct = default);

        Task<LoiComponentDTO> UpdateComponentAsync(
            Guid ruleSetId, Guid componentId, UpdateLoiComponentDTO dto, Guid actor, CancellationToken ct = default);

        Task DeleteComponentAsync(Guid ruleSetId, Guid componentId, Guid actor, CancellationToken ct = default);

        Task<LoiMatrixDTO> GetMatrixAsync(Guid ruleSetId, Guid componentId, CancellationToken ct = default);

        Task<LoiMatrixDTO> SaveMatrixAsync(
            Guid ruleSetId, Guid componentId, SaveLoiMatrixDTO dto, Guid actor, CancellationToken ct = default);

        Task<LoiMatrixDTO> RenameVariantAsync(
            Guid ruleSetId, Guid componentId, RenameLoiVariantDTO dto, Guid actor, CancellationToken ct = default);

        Task<LoiMatrixDTO> DeleteVariantAsync(
            Guid ruleSetId, Guid componentId, string? variant, Guid actor, CancellationToken ct = default);

        Task<IReadOnlyList<LoiParameterDTO>> GetParametersAsync(Guid ruleSetId, CancellationToken ct = default);

        Task<LoiParameterDTO> CreateParameterAsync(
            Guid ruleSetId, CreateLoiParameterDTO dto, Guid actor, CancellationToken ct = default);

        Task<LoiParameterDTO> UpdateParameterAsync(
            Guid ruleSetId, Guid parameterId, UpdateLoiParameterDTO dto, Guid actor, CancellationToken ct = default);

        Task DeleteParameterAsync(Guid ruleSetId, Guid parameterId, Guid actor, CancellationToken ct = default);

        Task<LoiRuleSetDTO?> GetProjectRuleSetAsync(Guid projectId, CancellationToken ct = default);
    }
}
