namespace Application.DTOs.ResponseDTOs.Project
{
    // Kết quả AI đọc file BEP -> các field prefill cho stepper khởi tạo dự án.
    // Chỉ là gợi ý điền hộ, không tạo dự án. Người dùng vẫn kiểm tra & bấm khởi tạo.
    public class BepParseResultDTO
    {
        // Step 1 — Thông tin dự án
        public string? ProjectName { get; set; }
        public string? ProjectCode { get; set; }
        public string? ProjectDescription { get; set; }
        public string? OwnerOrganizationName { get; set; }   // tên chủ đầu tư (text) — FE match sang Org Guid
        public string? ContactAddress { get; set; }
        public string? Address { get; set; }                 // địa điểm công trình

        // Step 4/5 — Nhóm/bên tham gia
        public List<BepGroupDTO> Groups { get; set; } = new();

        // Step 3 — Gói thầu (có thể rỗng)
        public List<BepPackageDTO> Packages { get; set; } = new();

        // true nếu PDF không trích được chữ (scan/lỗi) -> FE báo người dùng nhập tay.
        public bool ExtractionEmpty { get; set; }
    }    
}
