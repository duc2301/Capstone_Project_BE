using Application.DTOs.ResponseDTOs.Notification;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;

namespace Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationPusher _pusher;
        private readonly ICurrentUserService _currentUser;

        public NotificationService(
            IUnitOfWork unitOfWork,
            INotificationPusher pusher,
            ICurrentUserService currentUser)
        {
            _unitOfWork = unitOfWork;
            _pusher = pusher;
            _currentUser = currentUser;
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
            var existing = (await _unitOfWork.Repository<Account>().GetAllAsync())
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

        public async Task<IEnumerable<NotificationResponseDTO>> GetMyAsync()
        {
            var accountId = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var notis = (await _unitOfWork.Repository<Notification>().GetAllAsync())
                .Where(n => n.AccountId == accountId)
                .OrderByDescending(n => n.SendAt)
                .Select(ToDto);

            return notis;
        }

        public async Task MarkReadAsync(Guid notificationId)
        {
            var accountId = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

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
