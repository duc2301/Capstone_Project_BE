using Application.DTOs.ResponseDTOs.Issue;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Issue;

namespace Application.Services
{
    public class IssueActivityService : IIssueActivityService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLog;
        private readonly IIssueBroadcaster _issueBroadcaster;

        public IssueActivityService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IAuditLogService auditLog,
            IIssueBroadcaster issueBroadcaster)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _auditLog = auditLog;
            _issueBroadcaster = issueBroadcaster;
        }

        public async Task MarkInProgressOnActivityAsync(Guid issueId, Guid actorId)
        {
            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId);
            if (issue == null || issue.Status != IssueStatus.Open) return;
            if (issue.RaisedByAccountId.HasValue && issue.RaisedByAccountId.Value == actorId) return;

            issue.Status = IssueStatus.InProgress;
            issue.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Issue>().Update(issue);

            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.StatusChange, nameof(Issue), issue.Id.ToString(), actorId,
                detail: $"Vấn đề '{issue.Title}': Mở -> Đang xử lý",
                projectId: issue.ProjectId);
            await _unitOfWork.CommitAsync();

            if (issue.LinkedFileItemId.HasValue)
            {
                await _issueBroadcaster.IssueUpdatedAsync(
                    issue.LinkedFileItemId.Value, _mapper.Map<IssueResponseDTO>(issue));
            }
        }
    }
}
