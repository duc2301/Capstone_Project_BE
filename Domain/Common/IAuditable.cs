namespace Domain.Common
{
    // Entity có mốc thời gian tạo/cập nhật -> service tự set khi Create/Update
    public interface IAuditable
    {
        DateTime? CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
}
