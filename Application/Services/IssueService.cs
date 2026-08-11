using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Common;
using Application.DTOs.ResponseDTOs.Issue;
using Application.ExceptionMiddleware;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.Discussion;
using Domain.Enum.Group;
using Domain.Enum.Issue;
using Domain.Enum.Permission;
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
        // Data-only folder-tree access (project existence, folder names). Permission decisions go
        // through _permission.
        private readonly IFolderTreeRepository _folderTreeRepository;
        private readonly IPermissionCheckingService _permission;
        private readonly IAuditLogService _auditLog;
        private readonly IProjectFlowService _projectFlow;

        public IssueService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileZoneResolverService zoneResolver,
            IDiscussionService discussionService,
            INotificationService notification,
            IIssueBroadcaster issueBroadcaster,
            IFileStorageService storage,
            IFolderTreeRepository folderTreeRepository,
            IPermissionCheckingService permission,
            IAuditLogService auditLog,
            IProjectFlowService projectFlow)
        {
            _auditLog = auditLog;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _zoneResolver = zoneResolver;
            _discussionService = discussionService;
            _notification = notification;
            _issueBroadcaster = issueBroadcaster;
            _storage = storage;
            _folderTreeRepository = folderTreeRepository;
            _permission = permission;
            _projectFlow = projectFlow;
        }

        private async Task<bool> CanViewIssueTargetAsync(Issue issue, Guid accountId)
        {
            if (issue.LinkedFileItemId.HasValue)
                return await _permission.HasViewFileAsync(issue.LinkedFileItemId.Value, accountId);

            if (issue.LinkedFolderId.HasValue)
                return await _permission.HasViewFolderAsync(issue.LinkedFolderId.Value, accountId);

            return false;
        }

        private async Task<bool> IsIssueStakeholderAsync(Issue issue, Guid accountId)
        {
            if (issue.RaisedByAccountId == accountId || issue.AssignedToAccountId == accountId) return true;

            var mentions = await _unitOfWork.Repository<IssueMention>()
                .FindAsync(m => m.IssueId == issue.Id && m.MentionedAccountId == accountId);
            return mentions.Any();
        }

        public async Task<IEnumerable<IssueResponseDTO>> GetByFileItemAsync(Guid fileItemId, Guid accountId)
        {
            _ = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            if (!await _permission.HasViewFileAsync(fileItemId, accountId))
                throw new ApiExceptionResponse("You do not have permission to view issues of this file.", 403);

            var issues = (await _unitOfWork.Repository<Issue>().FindAsync(i => i.LinkedFileItemId == fileItemId))
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
            var dtos = _mapper.Map<List<IssueResponseDTO>>(issues);

            var accountIds = issues
                .SelectMany(i => new[] { i.RaisedByAccountId, i.AssignedToAccountId })
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            if (accountIds.Count == 0) return dtos;

            var names = (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                .ToDictionary(a => a.Id, a => a.UserName);

            foreach (var dto in dtos)
            {
                if (dto.RaisedByAccountId.HasValue && names.TryGetValue(dto.RaisedByAccountId.Value, out var raised))
                    dto.RaisedByName = raised;
                if (dto.AssignedToAccountId.HasValue && names.TryGetValue(dto.AssignedToAccountId.Value, out var assigned))
                    dto.AssignedToName = assigned;
            }

            return dtos;
        }

        public async Task<IEnumerable<ProjectIssueListItemDTO>> GetByProjectAsync(
            Guid projectId, Guid accountId)
        {
            if (!await _folderTreeRepository.ProjectExistsAsync(projectId))
                throw new ApiExceptionResponse("Project not found.", 404);

            return await BuildProjectIssuesAsync(projectId, null, accountId);
        }

        public async Task<IEnumerable<ProjectIssueListItemDTO>> GetForMyProjectsAsync(Guid accountId)
        {
            var myProjects = await _projectFlow.GetMyProjectsAsync(accountId);
            if (myProjects.Count == 0) return Array.Empty<ProjectIssueListItemDTO>();

            var all = new List<ProjectIssueListItemDTO>();
            foreach (var project in myProjects)
                all.AddRange(await BuildProjectIssuesAsync(project.Id, project.ProjectName, accountId));

            return all.OrderByDescending(i => i.CreatedAt).ToList();
        }

        private async Task<List<ProjectIssueListItemDTO>> BuildProjectIssuesAsync(
            Guid projectId, string? projectName, Guid accountId)
        {
            var issues = (await _unitOfWork.Repository<Issue>().FindAsync(i => i.ProjectId == projectId)).ToList();
            if (issues.Count == 0) return new List<ProjectIssueListItemDTO>();

            var fileIds = issues.Where(i => i.LinkedFileItemId.HasValue)
                .Select(i => i.LinkedFileItemId!.Value).ToHashSet();
            var fileById = fileIds.Count == 0
                ? new Dictionary<Guid, FileItem>()
                : (await _unitOfWork.Repository<FileItem>().FindAsync(f => fileIds.Contains(f.Id)))
                    .ToDictionary(f => f.Id);

            var projectFolders = await _folderTreeRepository.GetProjectFoldersAsync(projectId, null);
            var folderNameById = projectFolders.ToDictionary(f => f.Id, f => f.Name);
            var folderAreaById = projectFolders.ToDictionary(f => f.Id, f => f.Area);

            var hasFullAccess = await _permission.HasProjectFullAccessAsync(projectId, accountId);
            var isSystemAdmin = await _permission.HasSystemAdminAsync(accountId);
            var viewableFolderIds = await _permission.GetViewableFolderIdsAsync(projectId, accountId);

            Guid? FolderOf(Issue issue)
                => issue.LinkedFileItemId.HasValue && fileById.TryGetValue(issue.LinkedFileItemId.Value, out var file)
                    ? file.FolderId
                    : issue.LinkedFolderId;

            bool CanSee(Issue issue)
            {
                if (isSystemAdmin) return true;

                var folderId = FolderOf(issue);
                if (!folderId.HasValue)
                    return issue.RaisedByAccountId == accountId || issue.AssignedToAccountId == accountId;

                if (viewableFolderIds.Contains(folderId.Value)) return true;

                var isWip = !folderAreaById.TryGetValue(folderId.Value, out var area) || area == CdeArea.Wip;
                return hasFullAccess && !isWip;
            }

            var visible = issues.Where(CanSee).ToList();
            if (visible.Count == 0) return new List<ProjectIssueListItemDTO>();

            var accountIds = visible
                .SelectMany(i => new[] { i.RaisedByAccountId, i.AssignedToAccountId })
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var accountNames = accountIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                    .ToDictionary(a => a.Id, a => a.UserName);

            string? NameOf(Guid? id)
                => id.HasValue && accountNames.TryGetValue(id.Value, out var name) ? name : null;

            return visible
                .OrderByDescending(i => i.CreatedAt)
                .Select(i =>
                {
                    var folderId = FolderOf(i);
                    return new ProjectIssueListItemDTO
                    {
                        Id = i.Id,
                        ProjectId = i.ProjectId,
                        ProjectName = projectName,
                        Type = i.Type,
                        Title = i.Title,
                        Description = i.Description,
                        Status = i.Status,
                        Priority = i.Priority,
                        RaisedByAccountId = i.RaisedByAccountId,
                        RaisedByName = NameOf(i.RaisedByAccountId),
                        AssignedToAccountId = i.AssignedToAccountId,
                        AssignedToName = NameOf(i.AssignedToAccountId),
                        DueDate = i.DueDate,
                        CreatedAt = i.CreatedAt,
                        UpdatedAt = i.UpdatedAt,
                        LinkedFileItemId = i.LinkedFileItemId,
                        LinkedFileName = i.LinkedFileItemId.HasValue
                            && fileById.TryGetValue(i.LinkedFileItemId.Value, out var f) ? f.Name : null,
                        LinkedFolderId = folderId,
                        LinkedFolderName = folderId.HasValue
                            && folderNameById.TryGetValue(folderId.Value, out var n) ? n : null,
                    };
                })
                .ToList();
        }

        public async Task<IssueResponseDTO?> GetByIdAsync(Guid id, Guid accountId)
        {
            var entity = await _unitOfWork.Repository<Issue>().GetByIdAsync(id);
            if (entity == null) return null;

            if (!await CanViewIssueTargetAsync(entity, accountId)
                && !await IsIssueStakeholderAsync(entity, accountId))
                throw new ApiExceptionResponse("You do not have permission to view this issue.", 403);

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
            entity.Status = IssueStatus.Open;
            if (entity.DueDate.HasValue)
                entity.DueDate = DateTime.SpecifyKind(entity.DueDate.Value, DateTimeKind.Utc);
            var now = DateTime.UtcNow;
            entity.CreatedAt = now;
            entity.UpdatedAt = now;

            await _unitOfWork.Repository<Issue>().CreateAsync(entity);
            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Create, nameof(Issue), entity.Id.ToString(), actorId,
                detail: $"Tạo vấn đề '{entity.Title}'",
                projectId: entity.ProjectId);
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
            else if (entity.AssignedToOrganizationId.HasValue)
            {
                await NotifyOrganizationLeadersAsync(entity, actorId);
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
            if (entity.DueDate.HasValue)
                entity.DueDate = DateTime.SpecifyKind(entity.DueDate.Value, DateTimeKind.Utc);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Issue>().Update(entity);
            await _unitOfWork.CommitAsync();

            var result = _mapper.Map<IssueResponseDTO>(entity);

            if (entity.LinkedFileItemId.HasValue)
                await _issueBroadcaster.IssueUpdatedAsync(entity.LinkedFileItemId.Value, result);

            return result;
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
            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.StatusChange, nameof(Issue), issue.Id.ToString(), actorId,
                detail: $"Đánh dấu đã giải quyết vấn đề '{issue.Title}'",
                projectId: issue.ProjectId);
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

        public async Task<IssueResponseDTO> StartProgressAsync(Guid issueId, Guid actorId)
            => await TransitionAsync(issueId, actorId, IssueStatus.InProgress,
                new[] { IssueStatus.Open, IssueStatus.Answered },
                "Only the assignee can start working on this issue.",
                title => $"Issue \"{title}\" da duoc nhan xu ly.");

        public async Task<IssueResponseDTO> MarkAnsweredAsync(Guid issueId, Guid actorId)
            => await TransitionAsync(issueId, actorId, IssueStatus.Answered,
                new[] { IssueStatus.Open, IssueStatus.InProgress },
                "Only the assignee can answer this issue.",
                title => $"Issue \"{title}\" da duoc phan hoi, cho nguoi tao xac nhan.");

        private async Task<IssueResponseDTO> TransitionAsync(
            Guid issueId, Guid actorId, IssueStatus target, IssueStatus[] allowedFrom,
            string forbiddenMessage, Func<string, string> notifyText)
        {
            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId)
                ?? throw new ApiExceptionResponse("Issue not found.", 404);

            if (issue.AssignedToAccountId.HasValue)
            {
                if (issue.AssignedToAccountId.Value != actorId)
                    throw new ApiExceptionResponse(forbiddenMessage, 403);
            }
            else
            {
                var participantIds = await GetIssueParticipantAccountIdsAsync(issue);
                if (!participantIds.Contains(actorId))
                    throw new ApiExceptionResponse(forbiddenMessage, 403);
            }

            if (!allowedFrom.Contains(issue.Status))
                throw new ApiExceptionResponse($"Cannot move this issue to {target} from {issue.Status}.", 400);

            var previous = issue.Status;
            issue.Status = target;
            issue.UpdatedAt = DateTime.UtcNow;
            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.StatusChange, nameof(Issue), issue.Id.ToString(), actorId,
                detail: $"Vấn đề '{issue.Title}': {previous} -> {target}",
                projectId: issue.ProjectId);
            await _unitOfWork.CommitAsync();

            var recipientIds = (await GetIssueParticipantAccountIdsAsync(issue))
                .Where(id => id != actorId)
                .ToList();
            if (recipientIds.Count > 0)
            {
                await _notification.NotifyManyAsync(
                    recipientIds, notifyText(issue.Title),
                    linkType: "Issue", linkId: issue.Id.ToString());
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

        public async Task<IEnumerable<ProjectIssueListItemDTO>> GetAssignedToMeAsync(Guid accountId)
        {
            var issues = (await _unitOfWork.Repository<Issue>().FindAsync(
                    i => i.AssignedToAccountId == accountId && i.Status != IssueStatus.Closed))
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
            if (issues.Count == 0) return Array.Empty<ProjectIssueListItemDTO>();

            var fileIds = issues.Where(i => i.LinkedFileItemId.HasValue)
                .Select(i => i.LinkedFileItemId!.Value).ToHashSet();
            var fileById = fileIds.Count == 0
                ? new Dictionary<Guid, FileItem>()
                : (await _unitOfWork.Repository<FileItem>().FindAsync(f => fileIds.Contains(f.Id)))
                    .ToDictionary(f => f.Id);

            var raisedByIds = issues.Where(i => i.RaisedByAccountId.HasValue)
                .Select(i => i.RaisedByAccountId!.Value).ToHashSet();
            var raisedByNames = raisedByIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Account>().FindAsync(a => raisedByIds.Contains(a.Id)))
                    .ToDictionary(a => a.Id, a => a.UserName);

            var folderIds = issues.Select(i =>
                    i.LinkedFileItemId.HasValue && fileById.TryGetValue(i.LinkedFileItemId.Value, out var f)
                        ? f.FolderId
                        : i.LinkedFolderId)
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var folderNameById = folderIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Folder>().FindAsync(f => folderIds.Contains(f.Id)))
                    .ToDictionary(f => f.Id, f => f.Name);

            return issues.Select(i =>
            {
                var folderId = i.LinkedFileItemId.HasValue
                        && fileById.TryGetValue(i.LinkedFileItemId.Value, out var file)
                    ? file.FolderId
                    : i.LinkedFolderId;

                return new ProjectIssueListItemDTO
                {
                    Id = i.Id,
                    ProjectId = i.ProjectId,
                    Type = i.Type,
                    Title = i.Title,
                    Description = i.Description,
                    Status = i.Status,
                    Priority = i.Priority,
                    RaisedByAccountId = i.RaisedByAccountId,
                    RaisedByName = i.RaisedByAccountId.HasValue
                        && raisedByNames.TryGetValue(i.RaisedByAccountId.Value, out var raised) ? raised : null,
                    AssignedToAccountId = i.AssignedToAccountId,
                    DueDate = i.DueDate,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt,
                    LinkedFileItemId = i.LinkedFileItemId,
                    LinkedFileName = i.LinkedFileItemId.HasValue
                        && fileById.TryGetValue(i.LinkedFileItemId.Value, out var linked) ? linked.Name : null,
                    LinkedFolderId = folderId,
                    LinkedFolderName = folderId.HasValue
                        && folderNameById.TryGetValue(folderId.Value, out var folderName) ? folderName : null,
                };
            }).ToList();
        }

        public async Task<IEnumerable<Guid>> GetOpenIssueFileIdsForAccountAsync(
            IEnumerable<Guid> fileItemIds, Guid accountId)
        {
            var requestedIds = fileItemIds.ToHashSet();
            if (requestedIds.Count == 0) return Enumerable.Empty<Guid>();

            var files = await _unitOfWork.Repository<FileItem>()
                .FindAsync(f => requestedIds.Contains(f.Id));

            var viewableFileIds = new List<Guid>();

            foreach (var file in files)
            {
                if (await _permission.HasViewFileAsync(file.Id, accountId))
                    viewableFileIds.Add(file.Id);
            }

            return await GetOpenIssueFileIdsAsync(viewableFileIds);
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

        private async Task NotifyOrganizationLeadersAsync(Issue issue, Guid actorId)
        {
            if (!issue.AssignedToOrganizationId.HasValue) return;

            var groupIds = (await _unitOfWork.Repository<Group>().FindAsync(
                    g => g.OrganizationId == issue.AssignedToOrganizationId.Value))
                .Select(g => g.Id)
                .ToHashSet();
            if (groupIds.Count == 0) return;

            var leaderIds = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                      && m.Role == GroupMemberRole.Leader
                      && m.Status == GroupMemberStatus.Active))
                .Select(m => m.AccountId)
                .Where(id => id != actorId)
                .Distinct()
                .ToList();
            if (leaderIds.Count == 0) return;

            await _notification.NotifyManyAsync(
                leaderIds,
                $"Đơn vị của bạn được giao issue \"{issue.Title}\".",
                linkType: "Issue",
                linkId: issue.Id.ToString());
        }

        public async Task<IEnumerable<AssignableOrganizationDTO>> GetAssignableOrganizationsAsync(Guid fileItemId)
        {
            var groupIds = await ResolveAssignableGroupIdsAsync(fileItemId);
            if (groupIds.Count == 0) return Enumerable.Empty<AssignableOrganizationDTO>();

            var groups = (await _unitOfWork.Repository<Group>().FindAsync(
                    g => groupIds.Contains(g.Id) && g.OrganizationId != null))
                .ToList();
            if (groups.Count == 0) return Enumerable.Empty<AssignableOrganizationDTO>();

            var organizationIds = groups.Select(g => g.OrganizationId!.Value).ToHashSet();
            var organizationsById = (await _unitOfWork.Repository<Organization>().FindAsync(
                    o => organizationIds.Contains(o.Id)))
                .ToDictionary(o => o.Id);

            return groups
                .Where(g => organizationsById.ContainsKey(g.OrganizationId!.Value))
                .GroupBy(g => g.OrganizationId!.Value)
                .Select(grp => new AssignableOrganizationDTO
                {
                    OrganizationId = grp.Key,
                    OrganizationName = organizationsById[grp.Key].DisplayName
                        ?? organizationsById[grp.Key].LegalName,
                    GroupNames = grp.Select(g => g.Name).OrderBy(n => n).ToList()
                })
                .OrderBy(o => o.OrganizationName)
                .ToList();
        }

        private async Task<HashSet<Guid>> ResolveAssignableGroupIdsAsync(Guid fileItemId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            var permittedParticipantIds = (await _unitOfWork.Repository<FolderPermission>().FindAsync(
                    fp => fp.FolderId == folder.Id
                       && fp.Status == PermissionStatus.Active
                       && fp.CanView
                       && fp.ProjectParticipantId != null))
                .Select(fp => fp.ProjectParticipantId!.Value)
                .ToHashSet();
            if (permittedParticipantIds.Count == 0) return new HashSet<Guid>();

            return (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => permittedParticipantIds.Contains(p.Id)
                      && p.Status == ProjectParticipantStatus.Active))
                .Select(p => p.GroupId)
                .ToHashSet();
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
