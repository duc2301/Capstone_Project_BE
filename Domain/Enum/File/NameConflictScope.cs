namespace Domain.Enum.File
{
    // Tên tài liệu đang bị chiếm ở đâu — quyết định người dùng còn lựa chọn nào.
    // Hai phạm vi này là hai luật khác nhau: cùng thư mục xét theo Name, khác thư mục xét theo
    // (Name + đuôi file) trong phạm vi dự án.
    public enum NameConflictScope
    {
        None = 0,        // tên còn trống -> tạo tài liệu mới bình thường
        SameFolder = 1,  // ngay trong thư mục đích -> chọn lên phiên bản hoặc tách tài liệu riêng
        OtherFolder = 2  // thư mục khác trong dự án -> không xử lý tại chỗ được, phải đổi tên/đưa về WIP
    }
}
