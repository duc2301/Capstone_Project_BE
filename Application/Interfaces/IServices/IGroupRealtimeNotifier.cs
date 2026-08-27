namespace Application.Interfaces.IServices
{
    // Báo cho chính người bị đổi vai trò biết ngay (không cần đợi tải lại trang) — để FE tự ẩn nút
    // Ký/Duyệt hoặc làm mới danh sách nếu họ vừa bị hạ xuống Member.
    public interface IGroupRealtimeNotifier
    {
        Task MemberRoleChangedAsync(Guid accountId, Guid groupId, string newRole);
    }
}
