namespace Application.Interfaces.IServices
{
    // Đọc thông tin user hiện tại từ JWT trong Authorization header.
    // Khi endpoint chưa gắn [Authorize] mà không có token -> mọi field là null.
    public interface ICurrentUserService
    {
        bool IsAuthenticated { get; }
        Guid? AccountId { get; }
        string? UserName { get; }
        string? Email { get; }
        string? SystemRole { get; }
        // Danh sách Group account thuộc (lấy từ claim "Group" multi-valued)
        IReadOnlyList<Guid> GroupMemberships { get; }
    }
}
