using Application.Interfaces.IRepositories;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class NotificationDigestRepository : INotificationDigestRepository
    {
        private readonly CDESystemDbContext _dbContext;

        public NotificationDigestRepository(CDESystemDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Notification>> GetPendingDigestNotificationsAsync(DateTime cutoff, CancellationToken ct = default)
        {
            return await _dbContext.Notifications
                .Include(n => n.Account)
                .Where(n => !n.IsRead && !n.IsEmailSent && n.SendAt <= cutoff)
                .Where(n => n.Account != null && n.Account.IsEmailVerified)
                .OrderBy(n => n.SendAt)
                .ToListAsync(ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
