using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class FileItemResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public string Name { get; set; } = null!;
        public FileType FileType { get; set; }
        public FileItemStatus Status { get; set; }
        public Guid? CurrentVersionId { get; set; }
        // Version hiện hành theo hệ versioning mới, vd "P01.02" / "C01" (null nếu chưa có nội dung)
        public string? DisplayVersion { get; set; }
        // Email người upload bản hiện hành
        public string? UploaderEmail { get; set; }
        // Dung lượng (byte) của bản hiện hành (null nếu chưa có nội dung)
        public long? FileSizeBytes { get; set; }
        // Định dạng file của bản hiện hành, vd "DWG", "RVT", "PDF" (null nếu chưa có nội dung)
        public string? Format { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        public bool? Warnning { get; set; }
        public string? WarnningMessage { get; set; }

        // Tóm tắt nội dung do AI sinh sau upload (null = chưa tóm tắt / không trích được chữ).
        public string? Description { get; set; }

        /// <summary>File dang co it nhat 1 Issue chua Closed (Open/InProgress/Answered).</summary>
        public bool HasOpenIssue { get; set; }
    }
}
