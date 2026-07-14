using Application.Services.Loi;

namespace Application.Interfaces.IServices
{
    public interface IIfcLoiExtractor
    {
        Task<IfcLoiModel> ExtractAsync(Stream ifcStream, CancellationToken ct = default);
    }
}
