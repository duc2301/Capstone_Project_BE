using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Group;
using Domain.Enum.Issue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    public class IssueDueDateBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IssueDueDateBackgroundService> _logger;
        private readonly TimeSpan _pollingInterval;
        private readonly TimeSpan _reminderLeadTime;

        public IssueDueDateBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<IssueDueDateBackgroundService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var pollingMinutes = configuration.GetValue("IssueDueDate:PollingIntervalMinutes", 30);
            var reminderLeadHours = configuration.GetValue("IssueDueDate:ReminderLeadHours", 24);
            _pollingInterval = TimeSpan.FromMinutes(pollingMinutes);
            _reminderLeadTime = TimeSpan.FromHours(reminderLeadHours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueIssuesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking issue due dates.");
                }

                await Task.Delay(_pollingInterval, stoppingToken);
            }
        }

        private async Task ProcessDueIssuesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notification = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTime.UtcNow;
            var reminderThreshold = now.Add(_reminderLeadTime);

            var issues = (await unitOfWork.Repository<Issue>().FindAsync(
                    i => i.DueDate != null
                      && i.Status != IssueStatus.Closed
                      && i.DueDate <= reminderThreshold
                      && (i.DueReminderSentAt == null || i.OverdueNotifiedAt == null)))
                .ToList();
            if (issues.Count == 0) return;

            foreach (var issue in issues)
            {
                var isOverdue = issue.DueDate <= now;
                if (isOverdue && issue.OverdueNotifiedAt != null) continue;
                if (!isOverdue && issue.DueReminderSentAt != null) continue;

                var recipientIds = await ResolveRecipientsAsync(unitOfWork, issue);
                if (recipientIds.Count > 0)
                {
                    var message = isOverdue
                        ? $"Vấn đề \"{issue.Title}\" đã quá hạn xử lý."
                        : $"Vấn đề \"{issue.Title}\" sắp đến hạn xử lý.";
                    await notification.NotifyManyAsync(
                        recipientIds, message, linkType: "Issue", linkId: issue.Id.ToString());
                }

                if (isOverdue)
                {
                    issue.OverdueNotifiedAt = now;
                    issue.DueReminderSentAt ??= now;
                }
                else
                {
                    issue.DueReminderSentAt = now;
                }

                unitOfWork.Repository<Issue>().Update(issue);
            }

            await unitOfWork.CommitAsync();
        }

        private static async Task<List<Guid>> ResolveRecipientsAsync(IUnitOfWork unitOfWork, Issue issue)
        {
            var recipientIds = new HashSet<Guid>();
            if (issue.RaisedByAccountId.HasValue) recipientIds.Add(issue.RaisedByAccountId.Value);
            if (issue.AssignedToAccountId.HasValue) recipientIds.Add(issue.AssignedToAccountId.Value);

            if (issue.AssignedToGroupId.HasValue)
            {
                var memberIds = (await unitOfWork.Repository<GroupMember>().FindAsync(
                        m => m.GroupId == issue.AssignedToGroupId.Value && m.Status == GroupMemberStatus.Active))
                    .Select(m => m.AccountId);
                recipientIds.UnionWith(memberIds);
            }

            return recipientIds.ToList();
        }
    }
}
