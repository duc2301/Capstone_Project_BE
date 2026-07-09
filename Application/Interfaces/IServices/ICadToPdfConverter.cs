namespace Application.Interfaces.IServices
{
    public interface ICadToPdfConverter
    {
        // true neu duoi file thuoc loai convert duoc (dwg/dwgx).
        bool CanConvert(string extension);
        Task<Stream> ConvertToPdfAsync(Stream content, string extension, CancellationToken ct = default);
    }
}
