using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ApiResponseDTO;
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
        private readonly ICdeStorageKeyBuilder _storageKey;
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
            ICdeStorageKeyBuilder storageKey,
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
            _storageKey = storageKey;
            _folderTreeRepository = folderTreeRepository;
            _permission = permission;
            _projectFlow = projectFlow;
        }

        private async Task<bool> CanViewIssueTargetAsync(Issue issue, Guid accountId)
        {
            if (issue.LinkedFileItemId.HasValue)
                return await _permission.HasViewFileAsync(issue.LinkedFileItemId.Value, accountId);

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
            await FillGroupNamesAsync(dtos);

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

        private async Task FillGroupNamesAsync(List<IssueResponseDTO> dtos)
        {
            var groupIds = dtos
                .Where(d => d.AssignedToGroupId.HasValue)
                .Select(d => d.AssignedToGroupId!.Value)
                .ToHashSet();
            if (groupIds.Count == 0) return;

            var groupNames = (await _unitOfWork.Repository<Group>().FindAsync(g => groupIds.Contains(g.Id)))
                .ToDictionary(g => g.Id, g => g.Name);

            foreach (var dto in dtos.Where(d => d.AssignedToGroupId.HasValue))
            {
                if (groupNames.TryGetValue(dto.AssignedToGroupId!.Value, out var name))
                    dto.AssignedToGroupName = name;
            }
        }

        private const int DefaultIssuePageSize = 20;
        private const int MaxIssuePageSize = 500;

        private sealed record VisibleProjectIssue(Issue Issue, string? ProjectName);

        public async Task<PagedResult<ProjectIssueListItemDTO>> GetByProjectAsync(
            Guid projectId, Guid accountId, int page, int pageSize)
        {
            if (!await _folderTreeRepository.ProjectExistsAsync(projectId))
                throw new ApiExceptionResponse("Project not found.", 404);

            var (visible, fileById, folderNameById) = await ResolveVisibleProjectIssuesAsync(projectId, null, accountId);
            return await PageProjectIssuesAsync(visible, fileById, folderNameById, page, pageSize);
        }

        public async Task<PagedResult<ProjectIssueListItemDTO>> GetForMyProjectsAsync(Guid accountId, int page, int pageSize)
        {
            var myProjects = await _projectFlow.GetMyProjectsAsync(accountId);
            if (myProjects.Count == 0)
                return new PagedResult<ProjectIssueListItemDTO>(new List<ProjectIssueListItemDTO>(), 0, 1, pageSize);

            var allVisible = new List<VisibleProjectIssue>();
            var fileById = new Dictionary<Guid, FileItem>();
            var folderNameById = new Dictionary<Guid, string>();

            foreach (var project in myProjects)
            {
                var (visible, projectFileById, projectFolderNameById) =
                    await ResolveVisibleProjectIssuesAsync(project.Id, project.ProjectName, accountId);
                allVisible.AddRange(visible);
                foreach (var kv in projectFileById) fileById[kv.Key] = kv.Value;
                foreach (var kv in projectFolderNameById) folderNameById[kv.Key] = kv.Value;
            }

            var ordered = allVisible.OrderByDescending(v => v.Issue.CreatedAt).ToList();
            return await PageProjectIssuesAsync(ordered, fileById, folderNameById, page, pageSize);
        }

        private async Task<(List<VisibleProjectIssue> Visible, Dictionary<Guid, FileItem> FileById, Dictionary<Guid, string> FolderNameById)>
            ResolveVisibleProjectIssuesAsync(Guid projectId, string? projectName, Guid accountId)
        {
            var issues = (await _unitOfWork.Repository<Issue>().FindAsync(i => i.ProjectId == projectId)).ToList();
            if (issues.Count == 0)
                return (new List<VisibleProjectIssue>(), new Dictionary<Guid, FileItem>(), new Dictionary<Guid, string>());

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
                    : null;

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

            var visible = issues.Where(CanSee)
                .Select(i => new VisibleProjectIssue(i, projectName))
                .ToList();

            return (visible, fileById, folderNameById);
        }

        private async Task<PagedResult<ProjectIssueListItemDTO>> PageProjectIssuesAsync(
            List<VisibleProjectIssue> visible,
            IReadOnlyDictionary<Guid, FileItem> fileById,
            IReadOnlyDictionary<Guid, string> folderNameById,
            int page,
            int pageSize)
        {
            var safePage = page < 1 ? 1 : page;
            var safeSize = pageSize < 1 || pageSize > MaxIssuePageSize ? DefaultIssuePageSize : pageSize;
            var pageItems = visible.Skip((safePage - 1) * safeSize).Take(safeSize).ToList();

            var items = await BuildProjectIssueDtosAsync(pageItems, fileById, folderNameById);
            return new PagedResult<ProjectIssueListItemDTO>(items, visible.Count, safePage, safeSize);
        }

        private async Task<List<ProjectIssueListItemDTO>> BuildProjectIssueDtosAsync(
            IReadOnlyCollection<VisibleProjectIssue> pageIssues,
            IReadOnlyDictionary<Guid, FileItem> fileById,
            IReadOnlyDictionary<Guid, string> folderNameById)
        {
            if (pageIssues.Count == 0) return new List<ProjectIssueListItemDTO>();

            var accountIds = pageIssues
                .SelectMany(v => new[] { v.Issue.RaisedByAccountId, v.Issue.AssignedToAccountId })
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var accountNames = accountIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                    .ToDictionary(a => a.Id, a => a.UserName);

            string? NameOf(Guid? id)
                => id.HasValue && accountNames.TryGetValue(id.Value, out var name) ? name : null;

            var assignedGroupIds = pageIssues
                .Where(v => v.Issue.AssignedToGroupId.HasValue)
                .Select(v => v.Issue.AssignedToGroupId!.Value)
                .ToHashSet();
            var groupNames = assignedGroupIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Group>().FindAsync(g => assignedGroupIds.Contains(g.Id)))
                    .ToDictionary(g => g.Id, g => g.Name);

            string? GroupNameOf(Guid? id)
                => id.HasValue && groupNames.TryGetValue(id.Value, out var name) ? name : null;

            return pageIssues.Select(v =>
            {
                var i = v.Issue;
                var folderId = i.LinkedFileItemId.HasValue && fileById.TryGetValue(i.LinkedFileItemId.Value, out var file)
                    ? file.FolderId
                    : (Guid?)null;
                return new ProjectIssueListItemDTO
                {
                    Id = i.Id,
                    ProjectId = i.ProjectId,
                    ProjectName = v.ProjectName,
                    Type = i.Type,
                    Title = i.Title,
                    Description = i.Description,
                    Status = i.Status,
                    Priority = i.Priority,
                    RaisedByAccountId = i.RaisedByAccountId,
                    RaisedByName = NameOf(i.RaisedByAccountId),
                    AssignedToAccountId = i.AssignedToAccountId,
                    AssignedToName = NameOf(i.AssignedToAccountId),
                    AssignedToGroupId = i.AssignedToGroupId,
                    AssignedToGroupName = GroupNameOf(i.AssignedToGroupId),
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
            }).ToList();
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
            await BuildAssignmentNamesAsync(dto, entity);
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
            if (!dto.LinkedFileItemId.HasValue)
                throw new ApiExceptionResponse("Issue must be linked to a file.", 400);

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

            if (dto.AssignedToGroupId.HasValue)
                await RequireAssignableGroupAsync(dto.ProjectId, dto.AssignedToGroupId.Value, dto.LinkedFileItemId);

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

            if (entity.AssignedToAccountId.HasValue)
            {
                await AddParticipantsAsync(entity.Id, new[] { entity.AssignedToAccountId.Value });
                await GrantIssueFileViewAsync(
                    entity.Id, entity.LinkedFileItemId!.Value,
                    new[] { entity.AssignedToAccountId.Value }, actorId);
                if (entity.AssignedToAccountId.Value != actorId)
                {
                    await _notification.NotifyAsync(
                        entity.AssignedToAccountId.Value,
                        $"Bạn được giao vấn đề \"{entity.Title}\".",
                        linkType: "Issue",
                        linkId: entity.Id.ToString());
                }
            }
            else if (entity.AssignedToGroupId.HasValue)
            {
                var memberIds = await GetActiveGroupMemberIdsAsync(entity.AssignedToGroupId.Value);
                await AddParticipantsAsync(entity.Id, memberIds);
                await GrantIssueFileViewAsync(entity.Id, entity.LinkedFileItemId!.Value, memberIds, actorId);

                var recipientIds = memberIds.Where(id => id != actorId).ToList();
                if (recipientIds.Count > 0)
                {
                    await _notification.NotifyManyAsync(
                        recipientIds,
                        $"Nhóm của bạn được giao vấn đề \"{entity.Title}\".",
                        linkType: "Issue",
                        linkId: entity.Id.ToString());
                }
            }

            var result = await BuildAssignmentNamesAsync(_mapper.Map<IssueResponseDTO>(entity), entity);

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

            // Issue đóng -> thu hồi grant xem file sinh ra từ issue này.
            await RevokeIssueFileViewAsync(issue.Id, actorId);
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
            else
            {
                throw new ApiExceptionResponse("Issue has no linked file to store attachment.", 400);
            }

            var objectName = await _storageKey.ForIssueAttachmentAsync(folder.Id, issueId, fileName);
            var stored = await _storage.SaveAsync(content, objectName);

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

        public async Task<PagedResult<ProjectIssueListItemDTO>> GetAssignedToMeAsync(Guid accountId, int page, int pageSize)
        {
            var issues = (await _unitOfWork.Repository<Issue>().FindAsync(
                    i => i.AssignedToAccountId == accountId && i.Status != IssueStatus.Closed))
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            var safePage = page < 1 ? 1 : page;
            var safeSize = pageSize < 1 || pageSize > MaxIssuePageSize ? DefaultIssuePageSize : pageSize;

            if (issues.Count == 0)
                return new PagedResult<ProjectIssueListItemDTO>(new List<ProjectIssueListItemDTO>(), 0, safePage, safeSize);

            var pageIssues = issues.Skip((safePage - 1) * safeSize).Take(safeSize).ToList();

            var fileIds = pageIssues.Where(i => i.LinkedFileItemId.HasValue)
                .Select(i => i.LinkedFileItemId!.Value).ToHashSet();
            var fileById = fileIds.Count == 0
                ? new Dictionary<Guid, FileItem>()
                : (await _unitOfWork.Repository<FileItem>().FindAsync(f => fileIds.Contains(f.Id)))
                    .ToDictionary(f => f.Id);

            var raisedByIds = pageIssues.Where(i => i.RaisedByAccountId.HasValue)
                .Select(i => i.RaisedByAccountId!.Value).ToHashSet();
            var raisedByNames = raisedByIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Account>().FindAsync(a => raisedByIds.Contains(a.Id)))
                    .ToDictionary(a => a.Id, a => a.UserName);

            var folderIds = pageIssues.Select(i =>
                    i.LinkedFileItemId.HasValue && fileById.TryGetValue(i.LinkedFileItemId.Value, out var f)
                        ? f.FolderId
                        : (Guid?)null)
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var folderNameById = folderIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _unitOfWork.Repository<Folder>().FindAsync(f => folderIds.Contains(f.Id)))
                    .ToDictionary(f => f.Id, f => f.Name);

            var items = pageIssues.Select(i =>
            {
                var folderId = i.LinkedFileItemId.HasValue
                        && fileById.TryGetValue(i.LinkedFileItemId.Value, out var file)
                    ? file.FolderId
                    : (Guid?)null;

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

            return new PagedResult<ProjectIssueListItemDTO>(items, issues.Count, safePage, safeSize);
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

        private async Task<List<Guid>> GetActiveGroupMemberIdsAsync(Guid groupId)
            => (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => m.GroupId == groupId && m.Status == GroupMemberStatus.Active))
                .Select(m => m.AccountId)
                .Distinct()
                .ToList();

        // Cấp quyền xem file được liên kết cho danh sách account (người được giao / ẢNH CHỤP thành viên
        // nhóm được giao). Đường Allow cộng thêm, độc lập ACL nhóm. Upsert-and-reactivate theo
        // (IssueId, AccountId) để không vi phạm unique index nếu grant đã tồn tại.
        private async Task GrantIssueFileViewAsync(
            Guid issueId, Guid fileItemId, IEnumerable<Guid> accountIds, Guid actorId)
        {
            var wanted = accountIds.Distinct().ToList();
            if (wanted.Count == 0) return;

            var existing = (await _unitOfWork.Repository<IssueFileViewGrant>().FindAsync(
                    g => g.IssueId == issueId && wanted.Contains(g.AccountId)))
                .ToDictionary(g => g.AccountId);

            var now = DateTime.UtcNow;
            var changed = false;
            foreach (var accountId in wanted)
            {
                if (existing.TryGetValue(accountId, out var grant))
                {
                    if (grant.Status != PermissionStatus.Active)
                    {
                        grant.Status = PermissionStatus.Active;
                        _unitOfWork.Repository<IssueFileViewGrant>().Update(grant);
                        changed = true;
                    }
                    continue;
                }

                await _unitOfWork.Repository<IssueFileViewGrant>().CreateAsync(new IssueFileViewGrant
                {
                    Id = Guid.NewGuid(),
                    IssueId = issueId,
                    FileItemId = fileItemId,
                    AccountId = accountId,
                    Status = PermissionStatus.Active,
                    CreatedAt = now
                });
                changed = true;
            }

            // Ghi nhật ký chia sẻ TRƯỚC CommitAsync để log và grant cùng một transaction.
            if (changed)
                await LogShareAsync(AuditAction.Share, fileItemId, actorId, wanted.Count,
                                    $"qua vấn đề #{issueId}");

            if (changed) await _unitOfWork.CommitAsync();
        }

        // Thu hồi toàn bộ grant sinh ra từ 1 issue (khi đóng issue). Vì grant nằm ở bảng riêng và khóa
        // theo IssueId nên không bao giờ chạm grant của luồng ký hay của issue khác trên cùng file.
        private async Task RevokeIssueFileViewAsync(Guid issueId, Guid actorId)
        {
            var grants = (await _unitOfWork.Repository<IssueFileViewGrant>()
                .FindAsync(g => g.IssueId == issueId)).ToList();
            if (grants.Count == 0) return;

            foreach (var grant in grants)
                _unitOfWork.Repository<IssueFileViewGrant>().Delete(grant);

            await LogShareAsync(AuditAction.RevokeShare, grants[0].FileItemId, actorId, grants.Count,
                                $"do đóng vấn đề #{issueId}");

            await _unitOfWork.CommitAsync();
        }

        // Nhật ký chia sẻ / thu hồi quyền xem tài liệu. Luôn kèm folderId vì bộ lọc quyền phía đọc
        // chạy theo FolderId — thiếu thì ghi xong nhưng thành viên không xem được dòng log của mình.
        private async Task LogShareAsync(
            AuditAction action, Guid fileItemId, Guid actorId, int accountCount, string reason)
        {
            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (file is null) return;

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(file.FolderId);
            var verb = action == AuditAction.Share ? "Chia sẻ" : "Thu hồi";

            await _auditLog.LogAsync(
                LogScope.Project, action, nameof(FileItem), file.Id.ToString(), actorId,
                detail: $"{verb} quyền xem '{file.Name}' của {accountCount} tài khoản ({reason})",
                projectId: folder?.ProjectId, folderId: file.FolderId);
        }

        private async Task AddParticipantsAsync(Guid issueId, IEnumerable<Guid> accountIds)
        {
            var wanted = accountIds.Distinct().ToList();
            if (wanted.Count == 0) return;

            var existing = (await _unitOfWork.Repository<IssueMention>().FindAsync(
                    m => m.IssueId == issueId && wanted.Contains(m.MentionedAccountId)))
                .Select(m => m.MentionedAccountId)
                .ToHashSet();

            var added = false;
            foreach (var accountId in wanted.Where(id => !existing.Contains(id)))
            {
                await _unitOfWork.Repository<IssueMention>().CreateAsync(new IssueMention
                {
                    Id = Guid.NewGuid(),
                    IssueId = issueId,
                    MentionedAccountId = accountId
                });
                added = true;
            }

            if (added) await _unitOfWork.CommitAsync();
        }

        private async Task RequireAssignableGroupAsync(Guid projectId, Guid groupId, Guid? fileItemId)
        {
            if (fileItemId.HasValue)
            {
                var assignableGroupIds = await ResolveAssignableGroupIdsAsync(fileItemId.Value);
                if (!assignableGroupIds.Contains(groupId))
                    throw new ApiExceptionResponse("This group has no access to the linked file.", 400);
                return;
            }

            var isProjectGroup = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == projectId
                      && p.GroupId == groupId
                      && p.Status == ProjectParticipantStatus.Active))
                .Any();
            if (!isProjectGroup)
                throw new ApiExceptionResponse("This group does not take part in the project.", 400);
        }

        private async Task<IssueResponseDTO> BuildAssignmentNamesAsync(IssueResponseDTO dto, Issue entity)
        {
            if (entity.AssignedToGroupId.HasValue)
            {
                var group = await _unitOfWork.Repository<Group>().GetByIdAsync(entity.AssignedToGroupId.Value);
                dto.AssignedToGroupName = group?.Name;
            }
            else if (entity.AssignedToOrganizationId.HasValue)
            {
                var organization = await _unitOfWork.Repository<Organization>()
                    .GetByIdAsync(entity.AssignedToOrganizationId.Value);
                dto.AssignedToOrganizationName = organization?.DisplayName ?? organization?.LegalName;
            }

            return dto;
        }

        public async Task<IEnumerable<AssignableGroupDTO>> GetAssignableGroupsAsync(Guid fileItemId)
        {
            var groupIds = await ResolveAssignableGroupIdsAsync(fileItemId);
            if (groupIds.Count == 0) return Enumerable.Empty<AssignableGroupDTO>();

            var groups = (await _unitOfWork.Repository<Group>().FindAsync(g => groupIds.Contains(g.Id))).ToList();
            if (groups.Count == 0) return Enumerable.Empty<AssignableGroupDTO>();

            var organizationIds = groups
                .Where(g => g.OrganizationId.HasValue)
                .Select(g => g.OrganizationId!.Value)
                .ToHashSet();
            var organizationsById = organizationIds.Count > 0
                ? (await _unitOfWork.Repository<Organization>().FindAsync(o => organizationIds.Contains(o.Id)))
                    .ToDictionary(o => o.Id)
                : new Dictionary<Guid, Organization>();

            var memberCountByGroup = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId) && m.Status == GroupMemberStatus.Active))
                .GroupBy(m => m.GroupId)
                .ToDictionary(g => g.Key, g => g.Select(m => m.AccountId).Distinct().Count());

            return groups
                .Select(g => new AssignableGroupDTO
                {
                    GroupId = g.Id,
                    GroupName = g.Name,
                    OrganizationName = g.OrganizationId.HasValue
                        && organizationsById.TryGetValue(g.OrganizationId.Value, out var organization)
                            ? organization.DisplayName ?? organization.LegalName
                            : null,
                    MemberCount = memberCountByGroup.TryGetValue(g.Id, out var count) ? count : 0
                })
                .OrderBy(g => g.GroupName)
                .ToList();
        }

        private async Task<HashSet<Guid>> ResolveAssignableGroupIdsAsync(Guid fileItemId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            return (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == folder.ProjectId
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
