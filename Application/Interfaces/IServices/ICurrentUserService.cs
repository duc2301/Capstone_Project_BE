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
        IReadOnlyList<DepartmentMembership> DepartmentMemberships { get; }
    }

    // 1 account có thể có nhiều "Employee" rows (mỗi row = 1 phòng ban + role)
    public record DepartmentMembership(Guid DepartmentId, string Role);
}
