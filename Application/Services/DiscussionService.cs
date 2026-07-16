using Application.DTOs.RequestDTOs.Discussion;
using Application.DTOs.ResponseDTOs.Common;
using Application.DTOs.ResponseDTOs.Discussion;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;
using Domain.Enum.Discussion;

namespace Application.Services
{
    public class DiscussionService : IDiscussionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationService _notification;

        public DiscussionService(IUnitOfWork unitOfWork, IMapper mapper, INotificationService notification)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _notification = notification;
        }

        public async Task<IEnumerable<DiscussionResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<DiscussionResponseDTO>>(
                await _unitOfWork.Repository<Discussion>().GetAllAsync());

        public async Task<DiscussionResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Discussion>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task<DiscussionResponseDTO> CreateAsync(CreateDiscussionDTO dto)
        {
            var entity = _mapper.Map<Discussion>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Discussion>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task<DiscussionResponseDTO> UpdateAsync(Guid id, UpdateDiscussionDTO dto)
        {
            var entity = await _unitOfWork.Repository<Discussion>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Discussion with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Discussion>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Discussion>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Discussion with ID {id} not found.", 404);
            _unitOfWork.Repository<Discussion>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }

        public async Task<DiscussionResponseDTO> CreateForScopeAsync(
            DiscussionScopeType scopeType, Guid scopeId, Guid projectId, string title, Guid actorId)
        {
            var now = DateTime.UtcNow;
            var entity = new Discussion
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = title,
                ScopeType = scopeType,
                ScopeId = scopeId,
                Status = DiscussionStatus.Open,
                CreatedByAccountId = actorId,
                CreatedAt = now
            };
            await _unitOfWork.Repository<Discussion>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task<DiscussionResponseDTO?> GetByScopeAsync(DiscussionScopeType scopeType, Guid scopeId)
        {
            var entity = (await _unitOfWork.Repository<Discussion>().FindAsync(
                    d => d.ScopeType == scopeType && d.ScopeId == scopeId))
                .FirstOrDefault();
            return entity == null ? null : _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task<IEnumerable<DiscussionMessageResponseDTO>> GetMessagesAsync(Guid discussionId)
        {
            var messages = (await _unitOfWork.Repository<DiscussionMessage>().FindAsync(
                    m => m.DiscussionId == discussionId))
                .OrderBy(m => m.CreatedAt)
                .ToList();
            if (messages.Count == 0)
                return Enumerable.Empty<DiscussionMessageResponseDTO>();

            var messageIds = messages.Select(m => m.Id).ToHashSet();
            var attachments = (await _unitOfWork.Repository<MessageAttachment>().FindAsync(
                    a => messageIds.Contains(a.DiscussionMessageId)))
                .ToList();
            var mentions = (await _unitOfWork.Repository<MessageMention>().FindAsync(
                    m => messageIds.Contains(m.DiscussionMessageId)))
                .ToList();

            var accountIds = messages.Select(m => m.AuthorAccountId)
                .Concat(mentions.Select(x => x.MentionedAccountId))
                .ToHashSet();
            var accountNames = await ResolveAccountNamesAsync(accountIds);

            return messages.Select(m => MapMessage(
                m,
                attachments.Where(a => a.DiscussionMessageId == m.Id).ToList(),
                mentions.Where(x => x.DiscussionMessageId == m.Id).Select(x => x.MentionedAccountId).ToList(),
                accountNames));
        }

        public async Task<DiscussionMessageResponseDTO> PostMessageAsync(
            Guid discussionId, PostDiscussionMessageDTO dto, Guid actorId)
        {
            var discussion = await _unitOfWork.Repository<Discussion>().GetByIdAsync(discussionId)
                ?? throw new ApiExceptionResponse("Discussion not found.", 404);

            if (discussion.ScopeType == DiscussionScopeType.Issue && discussion.ScopeId.HasValue)
                await RequireIssueMemberAsync(discussion.ScopeId.Value, actorId);

            var content = dto.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
                throw new ApiExceptionResponse("Content is required.", 400);

            if (dto.ReplyToMessageId.HasValue)
            {
                var replyTarget = await _unitOfWork.Repository<DiscussionMessage>().GetByIdAsync(dto.ReplyToMessageId.Value);
                if (replyTarget == null || replyTarget.DiscussionId != discussionId)
                    throw new ApiExceptionResponse("Reply target message not found in this discussion.", 400);
            }

            var now = DateTime.UtcNow;
            var message = new DiscussionMessage
            {
                Id = Guid.NewGuid(),
                DiscussionId = discussionId,
                Content = content,
                AuthorAccountId = actorId,
                ReplyToMessageId = dto.ReplyToMessageId,
                CreatedAt = now
            };
            await _unitOfWork.Repository<DiscussionMessage>().CreateAsync(message);

            var attachments = new List<MessageAttachment>();
            foreach (var a in dto.Attachments ?? new List<PostMessageAttachmentDTO>())
            {
                if (a.Type == MessageAttachmentType.File)
                {
                    if (!a.FileVersionId.HasValue)
                        throw new ApiExceptionResponse("FileVersionId is required for File attachment.", 400);
                    _ = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(a.FileVersionId.Value)
                        ?? throw new ApiExceptionResponse("File version not found.", 404);
                }
                else if (a.Type == MessageAttachmentType.CitedFolder)
                {
                    if (!a.FolderId.HasValue)
                        throw new ApiExceptionResponse("FolderId is required for CitedFolder attachment.", 400);
                    _ = await _unitOfWork.Repository<Folder>().GetByIdAsync(a.FolderId.Value)
                        ?? throw new ApiExceptionResponse("Folder not found.", 404);
                }
                else if (string.IsNullOrWhiteSpace(a.Url))
                {
                    throw new ApiExceptionResponse("Url is required for Image/Link attachment.", 400);
                }

                attachments.Add(new MessageAttachment
                {
                    Id = Guid.NewGuid(),
                    DiscussionMessageId = message.Id,
                    Type = a.Type,
                    FileVersionId = a.FileVersionId,
                    Url = a.Url,
                    FolderId = a.FolderId
                });
            }
            foreach (var attachment in attachments)
                await _unitOfWork.Repository<MessageAttachment>().CreateAsync(attachment);

            var mentionedIds = (dto.MentionedAccountIds ?? new List<Guid>()).Distinct().ToList();
            foreach (var accountId in mentionedIds)
            {
                await _unitOfWork.Repository<MessageMention>().CreateAsync(new MessageMention
                {
                    Id = Guid.NewGuid(),
                    DiscussionMessageId = message.Id,
                    MentionedAccountId = accountId
                });
            }

            await _unitOfWork.CommitAsync();

            if (mentionedIds.Count > 0)
            {
                await _notification.NotifyManyAsync(
                    mentionedIds,
                    $"Bạn được nhắc tới trong thảo luận \"{discussion.Title}\".",
                    linkType: "Discussion",
                    linkId: discussionId.ToString());
            }

            if (discussion.ScopeType == DiscussionScopeType.Issue && discussion.ScopeId.HasValue)
            {
                var alreadyNotified = mentionedIds.Append(actorId).ToHashSet();
                var replyRecipientIds = (await GetIssueParticipantAccountIdsAsync(discussion.ScopeId.Value))
                    .Where(id => !alreadyNotified.Contains(id))
                    .ToList();
                if (replyRecipientIds.Count > 0)
                {
                    await _notification.NotifyManyAsync(
                        replyRecipientIds,
                        $"Có tin nhắn mới trong thảo luận \"{discussion.Title}\".",
                        linkType: "Discussion",
                        linkId: discussionId.ToString());
                }
            }

            var accountNames = await ResolveAccountNamesAsync(
                mentionedIds.Append(actorId).ToHashSet());

            return MapMessage(message, attachments, mentionedIds, accountNames);
        }

        // Chi nguoi tao issue, nguoi thuc hien (AssignedTo) hoac nguoi duoc them tham gia (IssueMention)
        // moi duoc gui tin nhan trong thread thao luan cua issue — nguoi ngoai khong duoc "asign" thi khong
        // duoc thao luan, dung theo yeu cau nghiep vu.
        private async Task RequireIssueMemberAsync(Guid issueId, Guid actorId)
        {
            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId)
                ?? throw new ApiExceptionResponse("Issue not found.", 404);

            if (issue.RaisedByAccountId == actorId || issue.AssignedToAccountId == actorId)
                return;

            var isParticipant = (await _unitOfWork.Repository<IssueMention>().FindAsync(
                    m => m.IssueId == issueId && m.MentionedAccountId == actorId))
                .Any();
            if (isParticipant) return;

            throw new ApiExceptionResponse(
                "Only the issue creator, assignee, or added participants can post in this discussion.", 403);
        }

        /// <summary>Creator + assignee + toan bo participants (IssueMention) cua 1 issue - dung de bao reply thuong.</summary>
        private async Task<IReadOnlyCollection<Guid>> GetIssueParticipantAccountIdsAsync(Guid issueId)
        {
            var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId);
            if (issue == null)
                return Array.Empty<Guid>();

            var ids = new HashSet<Guid>();
            if (issue.RaisedByAccountId.HasValue) ids.Add(issue.RaisedByAccountId.Value);
            if (issue.AssignedToAccountId.HasValue) ids.Add(issue.AssignedToAccountId.Value);

            var mentionIds = (await _unitOfWork.Repository<IssueMention>().FindAsync(m => m.IssueId == issueId))
                .Select(m => m.MentionedAccountId);
            ids.UnionWith(mentionIds);

            return ids;
        }

        private async Task<IReadOnlyDictionary<Guid, string>> ResolveAccountNamesAsync(IEnumerable<Guid> accountIds)
        {
            var ids = accountIds.ToHashSet();
            if (ids.Count == 0) return new Dictionary<Guid, string>();

            return (await _unitOfWork.Repository<Account>().FindAsync(a => ids.Contains(a.Id)))
                .ToDictionary(a => a.Id, a => a.UserName);
        }

        private static DiscussionMessageResponseDTO MapMessage(
            DiscussionMessage message,
            List<MessageAttachment> attachments,
            List<Guid> mentionedIds,
            IReadOnlyDictionary<Guid, string> accountNames)
            => new()
            {
                Id = message.Id,
                DiscussionId = message.DiscussionId,
                Content = message.Content,
                AuthorAccountId = message.AuthorAccountId,
                AuthorName = accountNames.TryGetValue(message.AuthorAccountId, out var authorName) ? authorName : null,
                IsSolution = message.IsSolution,
                ReplyToMessageId = message.ReplyToMessageId,
                CreatedAt = message.CreatedAt,
                Attachments = attachments.Select(a => new MessageAttachmentResponseDTO
                {
                    Id = a.Id,
                    Type = a.Type,
                    FileVersionId = a.FileVersionId,
                    Url = a.Url,
                    FolderId = a.FolderId
                }).ToList(),
                Mentions = mentionedIds.Select(id => new AccountRefDTO
                {
                    AccountId = id,
                    Name = accountNames.TryGetValue(id, out var name) ? name : null
                }).ToList()
            };
    }
}
