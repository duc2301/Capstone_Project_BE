namespace Application.DTOs.ResponseDTOs.FileItem
{
    // Kết quả luồng upload: file đích + phiên bản hiện hành + có phải bản mới của file cũ không.
    public class FileUploadResultDTO
    {
        public FileItemResponseDTO FileItem { get; set; } = null!;
        public FileVersionResponseDTO Version { get; set; } = null!;
        public bool IsNewVersion { get; set; }

        // Link xem/tải tạm thời (pre-signed). null nếu provider không hỗ trợ (đĩa local -> dùng endpoint /download).
        public string? Url { get; set; }

        // Nếu là phiên bản mới: bản cũ được chuyển sang folder Archived dưới FileItem này.
        public Guid? ArchivedFileItemId { get; set; }
    }
}
