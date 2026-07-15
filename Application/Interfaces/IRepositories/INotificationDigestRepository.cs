using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    /// <summary>
    /// Repository chuyên biệt cho Background Service gửi email digest.
    /// Tách riêng để tầng Application không cần phụ thuộc vào DbContext.
    /// </summary>
    public interface INotificationDigestRepository
    {
        /// <summary>
        /// Lấy tất cả Notification chưa đọc, chưa gửi email, đã qua khoảng delay,
        /// kèm theo thông tin Account (đã xác thực email).
        /// </summary>
        Task<List<Notification>> GetPendingDigestNotificationsAsync(DateTime cutoff, CancellationToken ct = default);

        /// <summary>
        /// Lưu các thay đổi (IsEmailSent = true) vào database.
        /// </summary>
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
