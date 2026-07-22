using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class RelatedFileDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public FileType FileType { get; set; }
        public FileItemStatus Status { get; set; }

        public Guid FolderId { get; set; }
        public string FolderName { get; set; } = null!;

        public CdeArea Area { get; set; }

        public int CurrentVersionNumber { get; set; }
        // Chuỗi version theo hệ mới, vd "P01.02" / "C01" (null nếu file chưa có version state).
        public string? DisplayVersion { get; set; }
        public string? Format { get; set; }
        public long SizeBytes { get; set; }

        public DateTime LinkedAt { get; set; }
        public string? LinkedByName { get; set; }
    }
}
