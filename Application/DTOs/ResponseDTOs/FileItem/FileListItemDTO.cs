using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileItem
{
    // 1 dòng trong danh sách file của 1 folder (đã gộp version hiện hành + tác giả) cho bảng Tài liệu.
    public class FileListItemDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public string Name { get; set; } = null!;
        public FileType FileType { get; set; }
        public FileItemStatus Status { get; set; }
        public ZoneReturnRequestStatus? ReturnRequestStatus { get; set; }
        public string? ReturnTargetZone { get; set; }

        public Guid? CurrentVersionId { get; set; }
        public int CurrentVersionNumber { get; set; }
        // Chuỗi version theo hệ mới, vd "P01.02" / "C01"
        public string? DisplayVersion { get; set; }
        public long SizeBytes { get; set; }
        public string? Format { get; set; }

        public Guid? CreatedByAccountId { get; set; }
        public string? AuthorName { get; set; }
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
