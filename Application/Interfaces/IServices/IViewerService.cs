using Application.DTOs.ResponseDTOs.Viewer;

namespace Application.Interfaces.IServices
{
    public interface IViewerService
    {
        Task<ViewerTokenResponseDTO> GetViewerTokenAsync(CancellationToken ct = default);

        Task<UploadModelResponseDTO> UploadAndTranslateAsync(
            Stream content, string fileName, long? contentLength = null, CancellationToken ct = default);

        Task<TranslationStatusResponseDTO> GetStatusAsync(string urn, CancellationToken ct = default);
    }
}
