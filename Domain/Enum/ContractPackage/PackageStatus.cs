namespace Domain.Enum.ContractPackage
{
    public enum PackageStatus
    {
        Draft,       // Nháp / Đang cấu hình
        Pending,     // Chờ bắt đầu
        Active,      // Đang thực hiện
        Completed,   // Hoàn thành
        Suspended,   // Tạm dừng
        Reviewing    // Đang soát xét
    }
}
