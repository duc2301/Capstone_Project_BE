using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Common;
using Application.DTOs.ResponseDTOs.Issue;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.Discussion;
using Domain.Enum.Group;
using Domain.Enum.Issue;
using Domain.Enum.Project;

namespace Application.Services
{
    public class IssueService : IIssueService
    {
        private const long MaxAttachmentSizeBytes = 20 * 1024 * 1024; // 20MB, khop [RequestSizeLimit] o controller

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IFileZoneResolverService _zoneResolver;
        private readonly IDiscussionService _discussionService;
        private readonly INotificationService _notification;
        private readonly IIssueBroadcaster _issueBroadcaster;
        private readonly IFileStorageService _storage;

        public IssueService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileZoneResolverService zoneResolver,
            IDiscussionService discussionService,
            INotificationService notification,
            IIssueBroadcaster issueBroadcaster,
            IFileStorageService storage)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _zoneResolver = zoneResolver;
            _discussionService = discussionService;
            _notification = notification;
            _issueBroadcaster = issueBroadcaster;
            _storage = storage;
        }

        public async Task<IEnumerable<IssueResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<IssueResponseDTO>>(
                await _unitOfWork.Repository<Issue>().GetAllAsync());

        public async Task<IEnumerable<IssueResponseDTO>> GetByFileItemAsync(Guid fileItemId)
            => _mapper.Map<IEnumerable<IssueResponseDTO>>(
                (await _unitOfWork.Repository<Issue>().FindAsync(i => i.LinkedFileItemId == fileItemId))
                    .OrderByDescending(i => i.CreatedAt));

        public async Task<IssueResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Issue>().GetByIdAsync(id);
            if (entity == null) return null;

            var dto = _mapper.Map<IssueResponseDTO>(entity);

            var participantIds = (await _unitOfWork.Repository<IssueMention>().FindAsync(m => m.IssueId == id))
                .Select(m => m.MentionedAccountId)
                .ToList();

            var accountIdsToResolve = participantIds.ToHashSet();
            if (entity.RaisedByAccountId.HasValue) accountIdsToResolve.Add(entity.RaisedByAccountId.Value);
            if (entity.AssignedToAccountId.HasValue) accountIdsToResolve.Add(entity.AssignedToAccountId.Value);

            var accountNames = accountIdsToResolve.Count > 0
                ? (await _unitOfWork.Repository<Account>().FindAsync(a => accountIdsToResolve.Contains(a.Id)))
                    .ToDictionary(a => a.Id, a => a.UserName)
                : new Dictionary<Guid, string>();
            string? ResolveName(Guid? accountId) =>
                accountId.HasValue && accountNames.TryGetValue(accountId.Value, out var name) ? name : null;

            dto.RaisedByName = ResolveName(entity.RaisedByAccountId);
            dto.AssignedToName = ResolveName(entity.AssignedToAccountId);
            dto.Participants = participantIds.Select(pid => new AccountRefDTO
            {
                AccountId = pid,
                Name = ResolveName(pid)
            }).ToList();

            var attachments = (await _unitOfWork.Repository<IssueAttachment>().FindAsync(a => a.IssueId == id))
                .ToList();
            dto.Attachments = new List<IssueAttachmentResponseDTO>();
            foreach (var attachment in attachments)
                dto.Attachments.Add(await BuildAttachmentDtoAsync(attachment));

            var discussion = (await _unitOfWork.Repository<Discussion>().FindAsync(
                    d => d.ScopeType == DiscussionScopeType.Issue && d.ScopeId == id))
                .FirstOrDefault();
            dto.DiscussionId = discussion?.Id;

            var latestReturnRequest = (await _unitOfWork.Repository<ZoneReturnRequest>().FindAsync(
                    r => r.IssueId == id))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            dto.LinkedReturnRequestStatus = latestReturnRequest?.Status.ToString();

            return dto;
        }

        public async Task<IssueResponseDTO> CreateAsync(CreateIssueDTO dto, Guid actorId)
        {
            if (dto.LinkedFileItemId.HasValue)
            {
                var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(dto.LinkedFileItemId.Value)
                    ?? throw new ApiExceptionResponse("Linked file not found.", 404);
                var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                    ?? throw new ApiExceptionResponse("File folder not found.", 404);
                if (folder.Area != CdeArea.Shared && folder.Area != CdeArea.Published)
                    throw new ApiExceptionResponse("Issue can only be created for files in Shared or Published zone.", 400);
                if (folder.Area == CdeArea.Published)
                {
                    var projectFolders = await _zoneResolver.GetProjectFoldersAsync(folder.ProjectId);
                    var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(fileItem, folder, projectFolders);
                    await _zoneResolver.RequireActiveTeamLeaderAsync(
                        actorId, teamGroupIds, "Only an active Team Leader can create an issue for files in the Published zone.");
                }
            }

            var entity = _mapper.Map<Issue>(dto);
            entity.Id = Guid.NewGuid();
            entity.RaisedByAccountId = actorId;
            var now = DateTime.UtcNow;
            entity.CreatedAt = now;
            entity.UpdatedAt = now;

            await _unitOfWork.Repository<Issue>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();

            await _discussionService.CreateForScopeAsync(
                DiscussionScopeType.Issue, entity.Id, entity.ProjectId, entity.Title, actorId);

            if (entity.AssignedToAccountId.HasValue && entity.AssignedToAccountId.Value != actorId)
            {
                await _notification.NotifyAsync(
                    entity.AssignedToAccountId.Value,
                    $"Bạn được gán issue \"{entity.Title}\".",
                    linkType: "Issue",
                    linkId: entity.Id.ToString());
            }

            var result = _mapper.Map<IssueResponseDTO>(entity);

            if (entity.LinkedFileItemId.HasValue)
                await _issueBroadcaster.IssueCreatedAsync(entity.LinkedFileItemId.Value, result);

            return result;
        }

        public async Task<IssueResponseDTO> UpdateAsync(Guid id, UpdateIssueDTO dto)
        {
            var entity = await _unitOfWork.Repository<Issue>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Issue with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Issue>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<IssueResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Issue>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Issue with ID {id} not found.", 404);
            _unitOfWork.Repository<Issue>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }

        public async Task<IssueResponseDTO> ResolveAsync(Guid issueId, Guid actorId)
        {
            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId)
                ?? throw new ApiExceptionResponse("Issue not found.", 404);
            RequireCreator(issue, actorId, "Only the issue creator can mark this issue resolved.");

            issue.Status = IssueStatus.Closed;
            issue.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.CommitAsync();
            var discussion = (await _unitOfWork.Repository<Discussion>().FindAsync(
                    d => d.ScopeType == DiscussionScopeType.Issue && d.ScopeId == issueId))
                .FirstOrDefault();
            if (discussion != null)
            {
                discussion.Status = DiscussionStatus.Resolved;
                await _unitOfWork.CommitAsync();
            }

            var recipientIds = (await GetIssueParticipantAccountIdsAsync(issue))
                .Where(id => id != actorId)
                .ToList();
            if (recipientIds.Count > 0)
            {
                await _notification.NotifyManyAsync(
                    recipientIds,
                    $"Issue \"{issue.Title}\" đã được đánh dấu giải quyết.",
                    linkType: "Issue",
                    linkId: issue.Id.ToString());
            }

            var result = _mapper.Map<IssueResponseDTO>(issue);

            if (issue.LinkedFileItemId.HasValue)
                await _issueBroadcaster.IssueUpdatedAsync(issue.LinkedFileItemId.Value, result);

            return result;
        }

        /// <summary>Creator + assignee + toan bo participants (IssueMention) cua 1 issue.</summary>
        private async Task<IReadOnlyCollection<Guid>> GetIssueParticipantAccountIdsAsync(Issue issue)
        {
            var ids = new HashSet<Guid>();
            if (issue.RaisedByAccountId.HasValue) ids.Add(issue.RaisedByAccountId.Value);
            if (issue.AssignedToAccountId.HasValue) ids.Add(issue.AssignedToAccountId.Value);

            var mentionIds = (await _unitOfWork.Repository<IssueMention>().FindAsync(m => m.IssueId == issue.Id))
                .Select(m => m.MentionedAccountId);
            ids.UnionWith(mentionIds);

            return ids;
        }

        public async Task<IEnumerable<Guid>> GetParticipantsAsync(Guid issueId)
            => (await _unitOfWork.Repository<IssueMention>().FindAsync(m => m.IssueId == issueId))
                .Select(m => m.MentionedAccountId);

        public async Task AddParticipantAsync(Guid issueId, Guid accountId, Guid actorId)
        {
            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId)
                ?? throw new ApiExceptionResponse("Issue not found.", 404);

            RequireCreator(issue, actorId, "Only the issue creator can add participants.");

            var exists = (await _unitOfWork.Repository<IssueMention>().FindAsync(
                    m => m.IssueId == issueId && m.MentionedAccountId == accountId))
                .Any();
            if (exists) return;

            await _unitOfWork.Repository<IssueMention>().CreateAsync(new IssueMention
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                MentionedAccountId = accountId
            });
            await _unitOfWork.CommitAsync();

            await _notification.NotifyAsync(
                accountId,
                $"Bạn được thêm vào issue \"{issue.Title}\".",
                linkType: "Issue",
                linkId: issueId.ToString());
        }

        public async Task RemoveParticipantAsync(Guid issueId, Guid accountId, Guid actorId)
        {
            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId)
                ?? throw new ApiExceptionResponse("Issue not found.", 404);

            RequireCreator(issue, actorId, "Only the issue creator can remove participants.");

            var mention = (await _unitOfWork.Repository<IssueMention>().FindAsync(
                    m => m.IssueId == issueId && m.MentionedAccountId == accountId))
                .FirstOrDefault();
            if (mention == null) return;

            _unitOfWork.Repository<IssueMention>().Delete(mention);
            await _unitOfWork.CommitAsync();
        }

        public async Task<IssueAttachmentResponseDTO> AddAttachmentAsync(
            Guid issueId, Stream content, string fileName, long fileSizeBytes, Guid actorId)
        {
            if (fileSizeBytes <= 0)
                throw new ApiExceptionResponse("No file provided.", 400);
            if (fileSizeBytes > MaxAttachmentSizeBytes)
                throw new ApiExceptionResponse("File exceeds the 20MB limit.", 400);

            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId)
                ?? throw new ApiExceptionResponse("Issue not found.", 404);

            await RequireCreatorOrLeaderAsync(
                issue, actorId, "Only the issue creator or an active Team Leader can add attachments.");

            Folder folder;
            if (issue.LinkedFileItemId.HasValue)
            {
                var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(issue.LinkedFileItemId.Value)
                    ?? throw new ApiExceptionResponse("Linked file not found.", 404);
                folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                    ?? throw new ApiExceptionResponse("File folder not found.", 404);
            }
            else if (issue.LinkedFolderId.HasValue)
            {
                folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(issue.LinkedFolderId.Value)
                    ?? throw new ApiExceptionResponse("Linked folder not found.", 404);
            }
            else
            {
                throw new ApiExceptionResponse("Issue has no linked file/folder to store attachment.", 400);
            }

            var extension = Path.GetExtension(fileName);
            var stored = await _storage.SaveAsync(content, folder.ProjectId, folder.Id, extension);

            var attachment = new IssueAttachment
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                Url = stored.RelativePath
            };
            await _unitOfWork.Repository<IssueAttachment>().CreateAsync(attachment);
            await _unitOfWork.CommitAsync();

            return await BuildAttachmentDtoAsync(attachment);
        }

        public async Task<IEnumerable<Guid>> GetOpenIssueFileIdsAsync(IEnumerable<Guid> fileItemIds)
        {
            var ids = fileItemIds.ToHashSet();
            if (ids.Count == 0) return Enumerable.Empty<Guid>();

            return (await _unitOfWork.Repository<Issue>().FindAsync(
                    i => i.LinkedFileItemId.HasValue
                         && ids.Contains(i.LinkedFileItemId.Value)
                         && i.Status != IssueStatus.Closed))
                .Select(i => i.LinkedFileItemId!.Value)
                .Distinct();
        }

        public async Task<IEnumerable<AssignableMemberDTO>> GetAssignableMembersAsync(Guid fileItemId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            var activeGroupIds = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == folder.ProjectId && p.Status == ProjectParticipantStatus.Active))
                .Select(p => p.GroupId)
                .ToHashSet();

            if (folder.Area == CdeArea.Wip)
            {
                var projectFolders = await _zoneResolver.GetProjectFoldersAsync(folder.ProjectId);
                var teamGroupIds = await _zoneResolver.ResolveTeamGroupIdsByFolderNameAsync(folder.ProjectId, folder, projectFolders);
                activeGroupIds.IntersectWith(teamGroupIds);
            }

            if (activeGroupIds.Count == 0) return Enumerable.Empty<AssignableMemberDTO>();

            var groupNameById = (await _unitOfWork.Repository<Group>().FindAsync(g => activeGroupIds.Contains(g.Id)))
                .ToDictionary(g => g.Id, g => g.Name);

            var members = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => activeGroupIds.Contains(m.GroupId) && m.Status == GroupMemberStatus.Active))
                .ToList();
            if (members.Count == 0) return Enumerable.Empty<AssignableMemberDTO>();

            var accountIds = members.Select(m => m.AccountId).ToHashSet();
            var accountsById = (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                .ToDictionary(a => a.Id);

            return members
                .Where(m => accountsById.ContainsKey(m.AccountId) && groupNameById.ContainsKey(m.GroupId))
                .Select(m => new AssignableMemberDTO
                {
                    AccountId = m.AccountId,
                    Name = accountsById[m.AccountId].UserName,
                    Email = accountsById[m.AccountId].Email,
                    GroupId = m.GroupId,
                    GroupName = groupNameById[m.GroupId]
                });
        }

        private async Task<IssueAttachmentResponseDTO> BuildAttachmentDtoAsync(IssueAttachment attachment)
            => new()
            {
                Id = attachment.Id,
                FileVersionId = attachment.FileVersionId,
                Url = !string.IsNullOrWhiteSpace(attachment.Url)
                    ? await _storage.GetPresignedUrlAsync(attachment.Url)
                    : null
            };

        // Chi nguoi tao issue (RaisedByAccountId) moi duoc thao tac — khong con cho phep Team Leader khac
        // "chen vao" giai quyet/quan ly participant ho, dung yeu cau nghiep vu "ai tao issue nguoi do xu ly".
        private static void RequireCreator(Issue issue, Guid actorId, string message)
        {
            if (issue.RaisedByAccountId != actorId)
                throw new ApiExceptionResponse(message, 403);
        }

        // Nguoi tao issue luon co quyen; neu khong phai nguoi tao thi phai la active Team Leader cua
        // team group phu trach file duoc lien ket (tai dung IFileZoneResolverService nhu ZoneReturnRequestService).
        // Van dung cho AddAttachmentAsync (chua doi, khong nam trong pham vi yeu cau nay).
        private async Task RequireCreatorOrLeaderAsync(Issue issue, Guid actorId, string message)
        {
            if (issue.RaisedByAccountId == actorId) return;

            if (!issue.LinkedFileItemId.HasValue)
                throw new ApiExceptionResponse(message, 403);

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(issue.LinkedFileItemId.Value)
                ?? throw new ApiExceptionResponse("Linked file not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(folder.ProjectId);
            var teamGroupIds = await _zoneResolver.ResolveFileTeamGroupIdsAsync(fileItem, folder, projectFolders);

            await _zoneResolver.RequireActiveTeamLeaderAsync(actorId, teamGroupIds, message);
        }
    }
}
