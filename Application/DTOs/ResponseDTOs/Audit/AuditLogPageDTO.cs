namespace Application.DTOs.ResponseDTOs.Audit
{
    // Kết quả phân trang cho nhật ký hoạt động (bảng log lớn nên luôn phân trang).
    public class AuditLogPageDTO
    {
        public List<AuditLogResponseDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
