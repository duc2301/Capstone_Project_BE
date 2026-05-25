namespace Domain.Common
{
    // Entity có mốc thời gian tạo/cập nhật -> GenericService tự set
    public interface IAuditable
    {
        DateTime? CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
}
