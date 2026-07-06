using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IFileContentReader
    {
        Task<ExtractedFile?> LoadTextAsync(Guid fileItemId, CancellationToken ct = default);

        public record ExtractedFile(FileItem Item, FileVersion Version, string Text);
    }
}
