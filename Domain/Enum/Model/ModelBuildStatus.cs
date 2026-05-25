namespace Domain.Enum.Model
{
    // Màu hiển thị mô hình — SUY RA từ WorkTask, không lưu cache (quyết định c)
    public enum ModelBuildStatus
    {
        NotBuilt,     // xám - chưa thi công
        InProgress,   // xanh - đang thi công
        Done          // đỏ - đã thi công xong
    }
}
