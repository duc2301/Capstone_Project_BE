using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;

namespace Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task NotifyAsync(
            Guid accountId,
            string message,
            string? senderName = null,
            string? linkType = null,
            string? linkId = null)
        {
            await _unitOfWork.Repository<Notification>().CreateAsync(
                BuildNotification(accountId, message, senderName, linkType, linkId, DateTime.UtcNow));
            await _unitOfWork.CommitAsync();
        }

        public async Task NotifyManyAsync(
            IEnumerable<Guid> accountIds,
            string message,
            string? senderName = null,
            string? linkType = null,
            string? linkId = null)
        {
            var now = DateTime.UtcNow;
            foreach (var accountId in accountIds.Distinct())
            {
                await _unitOfWork.Repository<Notification>().CreateAsync(
                    BuildNotification(accountId, message, senderName, linkType, linkId, now));
            }
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
    }
}
