namespace Application.DTOs.ResponseDTOs.Account
{
    // Kết quả import tài khoản hàng loạt từ file Excel (partial-success + báo cáo lỗi từng dòng).
    public class ImportAccountsResultDTO
    {
        public int TotalRows { get; set; }
        public int CreatedCount { get; set; }
        public int SkippedCount { get; set; }

        public List<ImportAccountRowErrorDTO> Errors { get; set; } = new();
        public List<ImportAccountCreatedDTO> Created { get; set; } = new();
    }

    public class ImportAccountRowErrorDTO
    {
        // Số dòng trong file Excel (1-based, khớp với dòng người dùng nhìn thấy).
        public int RowNumber { get; set; }
        public string? Email { get; set; }
        public string Reason { get; set; } = null!;
    }

    public class ImportAccountCreatedDTO
    {
        public int RowNumber { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
    }
}
