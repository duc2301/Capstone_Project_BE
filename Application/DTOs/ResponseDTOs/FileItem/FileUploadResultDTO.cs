namespace Application.DTOs.ResponseDTOs.FileItem
{
    // Kết quả luồng upload: file đích + bản ghi nội dung (content record) của nó.
    public class FileUploadResultDTO
    {
        public FileItemResponseDTO FileItem { get; set; } = null!;
        public FileVersionResponseDTO Version { get; set; } = null!;

        // Link xem/tải tạm thời (pre-signed). null nếu provider không hỗ trợ (đĩa local -> dùng endpoint /download).
        public string? Url { get; set; }
    }
}
