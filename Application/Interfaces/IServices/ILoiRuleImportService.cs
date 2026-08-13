using Application.DTOs.RequestDTOs.Loi;
using Application.DTOs.ResponseDTOs.Loi;

namespace Application.Interfaces.IServices
{
    public interface ILoiRuleImportService
    {
        Task<byte[]> GenerateTemplateAsync(Guid ruleSetId, CancellationToken ct = default);

        Task<LoiImportPreviewDTO> ParseAsync(Stream stream, Guid? ruleSetId, CancellationToken ct = default);

        Task<LoiRuleSetDTO> CommitAsync(LoiImportCommitDTO dto, Guid actor, CancellationToken ct = default);
    }
}
