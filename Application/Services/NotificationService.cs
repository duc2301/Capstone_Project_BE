using Application.DTOs.RequestDTOs.Notification;
using Application.DTOs.ResponseDTOs.Notification;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;

namespace Application.Services
{
    public class NotificationService : INotificationService
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationPusher _pusher;

        public NotificationService(
            IUnitOfWork unitOfWork,
            INotificationPusher pusher)
        {
            _unitOfWork = unitOfWork;
            _pusher = pusher;
        }

        public async Task NotifyAsync(
            Guid accountId,
            string message,
            string? senderName = null,
            string? linkType = null,
            string? linkId = null)
        {
            // Validate account tồn tại để tránh FK violation (sự cố 23503 trên Postgres)
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(accountId)
                ?? throw new ApiExceptionResponse($"Account {accountId} not found.", 404);

            var noti = BuildNotification(accountId, message, senderName, linkType, linkId, DateTime.UtcNow);
            await _unitOfWork.Repository<Notification>().CreateAsync(noti);
            await _unitOfWork.CommitAsync();

            await _pusher.PushAsync(accountId, ToDto(noti));
            _ = account;
        }

        public async Task NotifyManyAsync(
            IEnumerable<Guid> accountIds,
            string message,
            string? senderName = null,
            string? linkType = null,
            string? linkId = null)
        {
            var distinctIds = accountIds.Distinct().ToList();
            if (distinctIds.Count == 0) return;

            // Lọc bỏ id không tồn tại để tránh FK violation
            var existing = (await _unitOfWork.Repository<Account>()
                    .FindAsync(a => distinctIds.Contains(a.Id)))
                .Select(a => a.Id).ToHashSet();
            var validIds = distinctIds.Where(existing.Contains).ToList();
            if (validIds.Count == 0) return;

            var now = DateTime.UtcNow;
            var created = new List<Notification>();

            foreach (var accountId in validIds)
            {
                var noti = BuildNotification(accountId, message, senderName, linkType, linkId, now);
                await _unitOfWork.Repository<Notification>().CreateAsync(noti);
                created.Add(noti);
            }
            await _unitOfWork.CommitAsync();

            foreach (var noti in created)
                await _pusher.PushAsync(noti.AccountId, ToDto(noti));
        }

        public async Task<NotificationPageDTO> GetMyAsync(Guid accountId, NotificationFilterDTO filter)
        {
            var all = (await _unitOfWork.Repository<Notification>()
                    .FindAsync(n => n.AccountId == accountId))
                .OrderByDescending(n => n.SendAt)
                .ToList();

            var unreadCount = all.Count(n => !n.IsRead);
            var matched = all.Where(n => Matches(n, filter)).ToList();

            var safePage = filter.Page < 1 ? 1 : filter.Page;
            var safeSize = filter.PageSize < 1 || filter.PageSize > MaxPageSize
                ? DefaultPageSize
                : filter.PageSize;

            return new NotificationPageDTO
            {
                Items = matched.Skip((safePage - 1) * safeSize).Take(safeSize).Select(ToDto).ToList(),
                Total = matched.Count,
                UnreadCount = unreadCount,
                Page = safePage,
                PageSize = safeSize
            };
        }

        private static bool Matches(Notification noti, NotificationFilterDTO filter)
        {
            if (filter.UnreadOnly == true && noti.IsRead) return false;

            if (!string.IsNullOrWhiteSpace(filter.LinkType)
                && !string.Equals(noti.LinkType, filter.LinkType, StringComparison.OrdinalIgnoreCase))
                return false;

            if (filter.From.HasValue
                && noti.SendAt < DateTime.SpecifyKind(filter.From.Value, DateTimeKind.Utc))
                return false;

            if (filter.To.HasValue
                && noti.SendAt >= DateTime.SpecifyKind(filter.To.Value, DateTimeKind.Utc))
                return false;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                var inMessage = noti.Message?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
                var inSender = noti.SenderName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
                if (!inMessage && !inSender) return false;
            }

            return true;
        }

        public async Task<int> MarkAllReadAsync(Guid accountId)
        {
            var unread = (await _unitOfWork.Repository<Notification>()
                    .FindAsync(n => n.AccountId == accountId && !n.IsRead))
                .ToList();

            if (unread.Count == 0) return 0;

            foreach (var noti in unread)
                noti.IsRead = true;

            await _unitOfWork.CommitAsync();
            return unread.Count;
        }

        public async Task MarkReadAsync(Guid notificationId, Guid accountId)
        {
            var noti = await _unitOfWork.Repository<Notification>().GetByIdAsync(notificationId)
                ?? throw new ApiExceptionResponse("Notification not found.", 404);

            if (noti.AccountId != accountId)
                throw new ApiExceptionResponse("Cannot mark a notification of another user.", 403);

            noti.IsRead = true;
            await _unitOfWork.CommitAsync();
        }

        private static Notification BuildNotification(
            Guid accountId, string message, string? senderName,
            string? linkType, string? linkId, DateTime now) => new()
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Message = message,
                SenderName = senderName ?? "System",
                SendAt = now,
                IsRead = false,
                LinkType = linkType,
                LinkId = linkId
            };

        private static NotificationResponseDTO ToDto(Notification n) => new()
        {
            Id = n.Id,
            AccountId = n.AccountId,
            Message = n.Message,
            SendAt = n.SendAt,
            SenderName = n.SenderName,
            IsRead = n.IsRead,
            LinkType = n.LinkType,
            LinkId = n.LinkId
        };
    }
}
