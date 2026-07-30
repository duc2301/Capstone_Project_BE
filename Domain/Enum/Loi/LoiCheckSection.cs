namespace Domain.Enum.Loi
{
    // Các lớp kiểm chạy TUẦN TỰ; lớp trước hỏng thì lớp sau trả NotApplicable.
    // Tách ra để người dùng biết hỏng ở khâu nào thay vì chỉ thấy một con số phần trăm.
    public enum LoiCheckSection
    {
        Syntax = 1,

        Schema = 2,

        Classification = 3,

        RequiredFields = 4,

        GoodPractice = 5
    }
}
