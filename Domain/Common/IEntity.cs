namespace Domain.Common
{
    // Mọi entity có khóa chính Guid -> dùng cho generic CRUD
    public interface IEntity
    {
        Guid Id { get; set; }
    }
}
