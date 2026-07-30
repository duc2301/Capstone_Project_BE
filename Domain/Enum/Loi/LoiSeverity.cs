namespace Domain.Enum.Loi
{
    // Thứ tự tăng dần có chủ đích: trạng thái một lớp = mức CAO NHẤT của các quy tắc con.
    public enum LoiSeverity
    {
        // Chưa kiểm được (vd: không cấu kiện nào tra được bộ trường).
        NotApplicable = 0,

        // Đã chấm nhưng quy tắc chỉ mang tính thông tin.
        Applicable = 1,

        Passed = 2,

        // Khuyến nghị, KHÔNG làm model bị coi là không đạt.
        Warning = 3,

        Error = 4
    }
}
