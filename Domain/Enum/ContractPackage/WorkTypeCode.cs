namespace Domain.Enum.ContractPackage
{
    // Mã loại công việc của gói thầu. Lưu vào ContractPackage.WorkTypes dạng "STR,MEP".
    // Mã đầu tiên được dùng làm phần viết tắt trong mã gói thầu tự sinh.
    public static class WorkTypeCode
    {
        public const string General = "GEN";

        public static readonly string[] All =
        [
            "XDT",  // Xây dựng thô
            "STR",  // Kết cấu
            "ARC",  // Kiến trúc
            "MEP",  // Cơ điện
            "FIN",  // Hoàn thiện
            "RCC",  // Bê tông cốt thép
            "INF",  // Hạ tầng kỹ thuật
            "PCCC"  // Phòng cháy chữa cháy
        ];

        public static bool IsValid(string? code)
            => !string.IsNullOrWhiteSpace(code) && All.Contains(code);
    }
}
