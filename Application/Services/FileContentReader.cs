using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using System.Text;
using static Application.Interfaces.IServices.IFileContentReader;

namespace Application.Services
{
    public class FileContentReader : IFileContentReader
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IFileTextExtractor _extractor;

        public FileContentReader(IUnitOfWork unitOfWork, IFileStorageService storage, IFileTextExtractor extractor)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _extractor = extractor;
        }

        public async Task<ExtractedFile?> LoadTextAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var item = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (item == null || item.CurrentVersionId is null) return null;

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(item.CurrentVersionId);
            if (version == null) return null;

            await using var stream = await _storage.OpenReadAsync(version.StoragePath, ct);
            var text = await _extractor.ExtractTextAsync(stream, version.Format, ct);
            text = SanitizeText(text);

            return new ExtractedFile(item, version, text);
        }

        // Postgres text không nhận NUL (0x00). Bỏ NUL + control char C0, giữ \t \n \r + ký tự thường (kể cả tiếng Việt).
        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
                if (ch == '\t' || ch == '\n' || ch == '\r' || !char.IsControl(ch))
                    sb.Append(ch);
            return sb.ToString();
        }
    }
}
