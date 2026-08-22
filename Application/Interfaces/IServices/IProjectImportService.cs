using Application.DTOs.ResponseDTOs.Project;

namespace Application.Interfaces.IServices
{
    public interface IProjectImportService
    {
        Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);

        Task<ProjectImportPreviewDTO> ParseAsync(Stream stream, CancellationToken ct = default);
    }
}
