using Application.DTOs.RequestDTOs.Approval;
using Application.DTOs.ResponseDTOs.Approval;
using Application.ExceptionMiddleware;
using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Group;
using Domain.Enum.Project;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// Xử lý nghiệp vụ phê duyệt file trong hệ thống CDE.
    /// </summary>
    public class ApprovalService : IApprovalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileZoneResolverService _zoneResolver;
        private readonly ILogger<ApprovalService> _logger;
        private readonly IIngestBackgroundService _documentIngestBackgroundService;
        private readonly IFileVersionService _fileVersionService;
        private readonly INotificationService _notification;
        private readonly IApprovalRealtimeNotifier _approvalRealtime;

        public ApprovalService(
            IUnitOfWork unitOfWork,
            IFileZoneResolverService zoneResolver,
            ILogger<ApprovalService> logger,
            IIngestBackgroundService documentIngestBackgroundService,
            IFileVersionService fileVersionService,
            INotificationService notification,
            IApprovalRealtimeNotifier approvalRealtime)
        {
            _unitOfWork = unitOfWork;
            _zoneResolver = zoneResolver;
            _logger = logger;
            _documentIngestBackgroundService = documentIngestBackgroundService;
            _fileVersionService = fileVersionService;
            _notification = notification;
            _approvalRealtime = approvalRealtime;
        }

        #region API chính

        /// <summary>
        /// Gửi yêu cầu phê duyệt file. Chỉ member active trong team của file mới được thực hiện.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> SubmitAsync(
            Guid fileItemId,
            SubmitApprovalRequestDTO? dto,
            Guid actor)
        {
            var fileItem = await GetFileItemAsync(fileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);

            RequireCanSubmitApproval(fileItem, folder.Area);

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(
                fileItem,
                folder,
                requireApprovePermission: false);

            await RequireGroupMemberAsync(actor, teamGroupIds);
            var targetZone = ResolveApprovalTargetZone(dto?.TargetZone, folder.Area);
            RequireSignatureRulesForTransition(dto, folder.Area, targetZone);

            var hasPendingRequest = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.FileItemId == fileItem.Id && a.Status == ApprovalRequestStatus.Pending))
                .Any();
            if (hasPendingRequest)
                throw new ApiExceptionResponse("File is already pending approval.", 409);

            var hasPendingReturnRequest = (await _unitOfWork.Repository<ZoneReturnRequest>().FindAsync(
                    r => r.FileItemId == fileItem.Id && r.Status == ZoneReturnRequestStatus.Pending))
                .Any();
            if (hasPendingReturnRequest)
                throw new ApiExceptionResponse("File has a pending return to WIP request.", 409);

            var now = DateTime.UtcNow;
            var request = new ApprovalRequest
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItem.Id,
                RequestedBy = actor,
                FromZone = folder.Area,
                TargetZone = targetZone,
                RequiresSignature = dto?.RequiresSignature ?? false,
                Status = ApprovalRequestStatus.Pending,
                CreatedAt = now
            };

            var signers = await BuildApprovalSignersAsync(request, dto, teamGroupIds);

            fileItem.RequiresSignature = request.RequiresSignature;
            if (request.RequiresSignature)
                fileItem.IsSigned = false;
            fileItem.Status = FileItemStatus.PendingApproval;
            fileItem.UpdatedAt = now;

            await _unitOfWork.Repository<ApprovalRequest>().CreateAsync(request);
            foreach (var signer in signers)
                await _unitOfWork.Repository<ApprovalRequestSigner>().CreateAsync(signer);
            await _unitOfWork.CommitAsync();

            var leaderIds = (await GetActiveTeamLeaderAccountIdsAsync(teamGroupIds))
                .Where(id => id != actor)
                .ToList();

            var result = await BuildResponseAsync(request, fileItem);

            if (leaderIds.Count > 0)
            {
                await _notification.NotifyManyAsync(
                    leaderIds,
                    $"\"{fileItem.Name}\" cần bạn phê duyệt (chuyển từ {_zoneResolver.FormatZone(folder.Area)} sang {_zoneResolver.FormatZone(targetZone)}).",
                    linkType: "Approval",
                    linkId: request.Id.ToString());

                foreach (var leaderId in leaderIds)
                    await _approvalRealtime.ApprovalChangedAsync(leaderId, result);
            }

            return result;
        }

        /// <summary>
        /// Lấy tất cả approval request mà actor có quyền xem.
        /// </summary>
        public async Task<IEnumerable<ApprovalRequestResponseDTO>> GetAllAsync(Guid actor)
        {
            var requests = (await _unitOfWork.Repository<ApprovalRequest>().GetAllAsync())
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return await FilterVisibleRequestsAsync(requests, actor);
        }

        /// <summary>
        /// Lấy các approval request đang Pending mà actor có quyền xem.
        /// </summary>
        public async Task<IEnumerable<ApprovalRequestResponseDTO>> GetPendingAsync(Guid actor)
        {
            var pendingRequests = (await _unitOfWork.Repository<ApprovalRequest>().FindAsync(
                    a => a.Status == ApprovalRequestStatus.Pending))
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return await FilterVisibleRequestsAsync(pendingRequests, actor);
        }

        /// <summary>
        /// Lấy chi tiết approval request theo id. Actor phải là người gửi hoặc Team Leader.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> GetByIdAsync(Guid id, Guid actor)
        {
            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);

            if (!await CanViewRequestAsync(actor, request, fileItem))
                throw new ApiExceptionResponse("You do not have permission to view this approval request.", 403);

            return await BuildResponseAsync(request, fileItem);
        }

        /// <summary>
        /// Duyệt approval request. Chỉ Team Leader mới được thực hiện.
        /// Nếu file yêu cầu ký số thì phải hoàn tất ký trước khi duyệt.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> ApproveAsync(Guid id, Guid actor)
        {
            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);

            await RequireCanDecideAsync(actor, request, fileItem, folder);
            await RequireSignersCompleteBeforeApprovalAsync(request);

            var now = DateTime.UtcNow;
            request.Status = ApprovalRequestStatus.Approved;
            request.ApproverId = actor;
            request.ApprovedAt = now;
            request.RejectReason = null;

            fileItem.Status = FileItemStatus.Approved;
            fileItem.RequiresSignature = false;
            fileItem.UpdatedAt = now;

            await MoveApprovedFileToTargetZoneAsync(fileItem, folder, request.TargetZone, now);

            // Versioning: vào SHARED -> P{rev+1}.01, vào PUBLISHED -> C{pubRev+1} (dòng state mới).
            await ApplyZoneVersioningAsync(fileItem, request.TargetZone);

            await _unitOfWork.CommitAsync();

            if (request.TargetZone == CdeArea.Published)
                _documentIngestBackgroundService.Enqueue(fileItem.Id);

            var result = await BuildResponseAsync(request, fileItem);

            if (request.RequestedBy != actor)
            {
                await _notification.NotifyAsync(
                    request.RequestedBy,
                    $"\"{fileItem.Name}\" đã được duyệt và chuyển sang {_zoneResolver.FormatZone(request.TargetZone)}.",
                    linkType: "Approval",
                    linkId: request.Id.ToString());
            }

            await BroadcastApprovalChangedAsync(request, result, actor);

            return result;
        }

        /// <summary>Snapshot DTO hiện tại của 1 approval request — không check quyền, dùng nội bộ để broadcast realtime.</summary>
        public async Task<ApprovalRequestResponseDTO> GetSnapshotAsync(Guid approvalId)
        {
            var request = await GetRequestAsync(approvalId);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            return await BuildResponseAsync(request, fileItem);
        }

        /// <summary>
        /// Tất cả account có thể đang quan tâm/xem approval này: người gửi, Team Leader active của team
        /// sở hữu file, và mọi signer (account trực tiếp + member active của signer group).
        /// </summary>
        public async Task<IReadOnlyCollection<Guid>> GetStakeholderAccountIdsAsync(Guid approvalId)
        {
            var request = await GetRequestAsync(approvalId);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: true);

            var stakeholderIds = new HashSet<Guid> { request.RequestedBy };
            stakeholderIds.UnionWith(await GetActiveTeamLeaderAccountIdsAsync(teamGroupIds));

            var signers = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                    s => s.ApprovalRequestId == approvalId))
                .ToList();

            stakeholderIds.UnionWith(signers
                .Where(s => s.SignerAccountId.HasValue)
                .Select(s => s.SignerAccountId!.Value));

            var signerGroupIds = signers
                .Where(s => s.SignerGroupId.HasValue)
                .Select(s => s.SignerGroupId!.Value)
                .ToHashSet();
            if (signerGroupIds.Count > 0)
            {
                var groupMemberIds = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                        m => signerGroupIds.Contains(m.GroupId) && m.Status == GroupMemberStatus.Active))
                    .Select(m => m.AccountId);
                stakeholderIds.UnionWith(groupMemberIds);
            }

            return stakeholderIds;
        }

        /// <summary>Đẩy realtime state mới nhất của approval cho mọi stakeholder (trừ actor đang thao tác).</summary>
        private async Task BroadcastApprovalChangedAsync(ApprovalRequest request, ApprovalRequestResponseDTO snapshot, Guid actor)
        {
            var stakeholderIds = (await GetStakeholderAccountIdsAsync(request.Id))
                .Where(id => id != actor);
            foreach (var accountId in stakeholderIds)
                await _approvalRealtime.ApprovalChangedAsync(accountId, snapshot);
        }

        /// <summary>
        /// Bắt buộc actor là Team Leader active của team phụ trách file (vd: trước khi đặt vị trí chữ ký
        /// hoặc sinh PDF đã ký) — không cần ApprovalRequest đang Pending như RequireCanDecideAsync.
        /// </summary>
        public async Task RequireTeamLeaderAsync(Guid fileItemId, Guid actorId)
        {
            var fileItem = await GetFileItemAsync(fileItemId);
            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: true);
            if (!await IsGroupLeaderAsync(actorId, teamGroupIds))
                throw new ApiExceptionResponse("Only the Team Leader can perform this action.", 403);
        }

        /// <summary>
        /// Từ chối approval request. Chỉ Team Leader mới được thực hiện. Bắt buộc có lý do.
        /// </summary>
        public async Task<ApprovalRequestResponseDTO> RejectAsync(Guid id, RejectApprovalRequestDTO dto, Guid actor)
        {
            var reason = dto.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                throw new ApiExceptionResponse("Reject reason is required.", 400);

            var request = await GetRequestAsync(id);
            var fileItem = await GetFileItemAsync(request.FileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);

            await RequireCanDecideAsync(actor, request, fileItem, folder);

            var now = DateTime.UtcNow;
            request.Status = ApprovalRequestStatus.Rejected;
            request.ApproverId = actor;
            request.ApprovedAt = now;
            request.RejectReason = reason;

            fileItem.Status = FileItemStatus.Rejected;
            fileItem.RequiresSignature = false;
            fileItem.UpdatedAt = now;

            await _unitOfWork.CommitAsync();

            var result = await BuildResponseAsync(request, fileItem);

            if (request.RequestedBy != actor)
            {
                await _notification.NotifyAsync(
                    request.RequestedBy,
                    $"\"{fileItem.Name}\" bị từ chối duyệt: {reason}",
                    linkType: "Approval",
                    linkId: request.Id.ToString());
            }

            await BroadcastApprovalChangedAsync(request, result, actor);

            return result;
        }

        #endregion

        #region Lấy dữ liệu cơ bản

        /// <summary>Lấy FileItem theo id, ném 404 nếu không tìm thấy.</summary>
        private async Task<FileItem> GetFileItemAsync(Guid fileItemId)
            => await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
               ?? throw new ApiExceptionResponse("File not found.", 404);

        /// <summary>Lấy ApprovalRequest theo id, ném 404 nếu không tìm thấy.</summary>
        private async Task<ApprovalRequest> GetRequestAsync(Guid id)
            => await _unitOfWork.Repository<ApprovalRequest>().GetByIdAsync(id)
               ?? throw new ApiExceptionResponse("Approval request not found.", 404);

        /// <summary>Lấy Folder theo id, ném 404 nếu không tìm thấy.</summary>
        private async Task<Folder> GetFolderAsync(Guid folderId)
            => await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
               ?? throw new ApiExceptionResponse("File folder not found.", 404);

        #endregion

        #region Kiểm tra quyền

        /// <summary>
        /// Yêu cầu actor là Team Leader có quyền approve file và request đang ở trạng thái Pending.
        /// </summary>
        private async Task RequireCanDecideAsync(Guid actor, ApprovalRequest request, FileItem fileItem, Folder folder)
        {
            if (request.Status != ApprovalRequestStatus.Pending || fileItem.Status != FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("Only pending approval requests can be approved or rejected.", 409);

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, folder, requireApprovePermission: true);
            if (!await IsGroupLeaderAsync(actor, teamGroupIds))
                throw new ApiExceptionResponse("Only the Team Leader can approve or reject this file.", 403);
        }

        /// <summary>
        /// File chỉ được gửi duyệt ở WIP, Shared hoặc Published.
        /// Sau khi đã approve qua một vùng, muốn đi tiếp phải gửi request duyệt mới.
        /// </summary>
        private static void RequireCanSubmitApproval(FileItem fileItem, CdeArea currentZone)
        {
            if (currentZone == CdeArea.Archived)
                throw new ApiExceptionResponse("Archived file cannot be submitted for approval.", 400);

            if (fileItem.Status == FileItemStatus.PendingApproval)
                throw new ApiExceptionResponse("File is already pending approval.", 409);

            if (fileItem.Status == FileItemStatus.Rejected)
                throw new ApiExceptionResponse("Rejected file cannot be submitted for approval.", 400);
        }

        private static CdeArea ResolveApprovalTargetZone(string? targetZone, CdeArea currentZone)
        {
            var expectedZone = GetNextApprovalZone(currentZone);
            if (string.IsNullOrWhiteSpace(targetZone))
                return expectedZone;

            if (!Enum.TryParse<CdeArea>(targetZone.Trim(), ignoreCase: true, out var parsed))
                throw new ApiExceptionResponse("Invalid target zone.", 400);

            if (parsed != expectedZone)
                throw new ApiExceptionResponse($"Approval can only move file from {currentZone} to {expectedZone}.", 400);

            return parsed;
        }

        private static void RequireSignatureRulesForTransition(
            SubmitApprovalRequestDTO? dto,
            CdeArea currentZone,
            CdeArea targetZone)
        {
            if ((currentZone, targetZone) is not (CdeArea.Shared, CdeArea.Published))
                return;

            if (dto?.RequiresSignature != true)
                throw new ApiExceptionResponse("Shared to Published approval requires digital signature.", 400);

            var hasSignerAccounts = dto.SignerAccountIds.Any(id => id != Guid.Empty);
            var hasSignerGroups = dto.SignerGroupIds.Any(id => id != Guid.Empty);
            if (!hasSignerAccounts && !hasSignerGroups)
                throw new ApiExceptionResponse("Shared to Published approval requires at least one signer.", 400);
        }

        private async Task<IReadOnlyCollection<ApprovalRequestSigner>> BuildApprovalSignersAsync(
            ApprovalRequest request,
            SubmitApprovalRequestDTO? dto,
            IReadOnlyCollection<Guid> defaultTeamGroupIds)
        {
            if (!request.RequiresSignature)
                return Array.Empty<ApprovalRequestSigner>();

            var mustAssignExplicitSigners = request.FromZone == CdeArea.Shared
                                            && request.TargetZone == CdeArea.Published;
            var accountIds = mustAssignExplicitSigners
                ? (dto?.SignerAccountIds ?? new List<Guid>())
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList()
                : new List<Guid>();
            var groupIds = mustAssignExplicitSigners
                ? (dto?.SignerGroupIds ?? new List<Guid>())
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList()
                : defaultTeamGroupIds.ToList();

            if (!mustAssignExplicitSigners && accountIds.Count == 0 && groupIds.Count == 0)
                groupIds = defaultTeamGroupIds.ToList();

            if (accountIds.Count == 0 && groupIds.Count == 0)
                throw new ApiExceptionResponse("At least one signer is required when digital signature is required.", 400);

            await EnsureSignerAccountsExistAsync(accountIds);
            await EnsureSignerGroupsExistAsync(groupIds);

            return accountIds
                .Select(accountId => new ApprovalRequestSigner
                {
                    Id = Guid.NewGuid(),
                    ApprovalRequestId = request.Id,
                    SignerAccountId = accountId,
                    Status = ApprovalRequestSignerStatus.Pending
                })
                .Concat(groupIds.Select(groupId => new ApprovalRequestSigner
                {
                    Id = Guid.NewGuid(),
                    ApprovalRequestId = request.Id,
                    SignerGroupId = groupId,
                    Status = ApprovalRequestSignerStatus.Pending
                }))
                .ToList();
        }

        private async Task EnsureSignerAccountsExistAsync(IReadOnlyCollection<Guid> accountIds)
        {
            if (accountIds.Count == 0) return;

            var activeAccountIds = (await _unitOfWork.Repository<Account>().FindAsync(
                    a => accountIds.Contains(a.Id)))
                .Select(a => a.Id)
                .ToHashSet();
            if (accountIds.Any(id => !activeAccountIds.Contains(id)))
                throw new ApiExceptionResponse("One or more signer accounts do not exist.", 400);
        }

        private async Task EnsureSignerGroupsExistAsync(IReadOnlyCollection<Guid> groupIds)
        {
            if (groupIds.Count == 0) return;

            var existingGroupIds = (await _unitOfWork.Repository<Group>().FindAsync(
                    g => groupIds.Contains(g.Id)))
                .Select(g => g.Id)
                .ToHashSet();
            if (groupIds.Any(id => !existingGroupIds.Contains(id)))
                throw new ApiExceptionResponse("One or more signer groups do not exist.", 400);
        }

        /// <summary>
        /// Yêu cầu actor là member active trong team của file.
        /// </summary>
        private async Task RequireGroupMemberAsync(Guid accountId, IReadOnlyCollection<Guid> groupIds)
        {
            var isMember = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.AccountId == accountId
                         && m.Status == GroupMemberStatus.Active))
                .Any();
            if (!isMember)
                throw new ApiExceptionResponse("Only members of the file team can submit approval.", 403);
        }

        /// <summary>Kiểm tra actor có phải Group Leader active không.</summary>
        private async Task<bool> IsGroupLeaderAsync(Guid accountId, IReadOnlyCollection<Guid> groupIds)
            => (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.AccountId == accountId
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Any();

        /// <summary>Lấy AccountId của tất cả Team Leader active thuộc các group cho trước (dùng để báo có file cần duyệt).</summary>
        private async Task<IReadOnlyCollection<Guid>> GetActiveTeamLeaderAccountIdsAsync(IReadOnlyCollection<Guid> groupIds)
            => (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Select(m => m.AccountId)
                .Distinct()
                .ToList();

        /// <summary>
        /// Kiểm tra actor có quyền xem approval request không.
        /// Trả về true nếu actor là người gửi hoặc là Team Leader có quyền approve.
        /// </summary>
        private async Task<bool> CanViewRequestAsync(Guid actor, ApprovalRequest request, FileItem fileItem)
        {
            if (request.RequestedBy == actor)
                return true;

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, requireApprovePermission: true);
            return await IsGroupLeaderAsync(actor, teamGroupIds);
        }

        /// <summary>
        /// Kiểm tra chữ ký số trước khi approve chuyển file ra khỏi WIP.
        /// Các bước Shared -> Published và Published -> Archived không yêu cầu ký lại.
        /// </summary>
        private async Task RequireSignersCompleteBeforeApprovalAsync(ApprovalRequest request)
        {
            if (!request.RequiresSignature)
                return;

            if (IsExplicitSignerApproval(request))
            {
                var signers = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                        s => s.ApprovalRequestId == request.Id))
                    .ToList();

                if (signers.Count == 0 || signers.Any(s => s.Status != ApprovalRequestSignerStatus.Signed))
                    throw new ApiExceptionResponse("All required digital signers must sign before approval.", 409);

                return;
            }

            var hasSignedTransaction = (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == request.Id
                         && t.Status == SignatureTransactionStatus.Signed))
                .Any();
            if (!hasSignedTransaction)
                throw new ApiExceptionResponse("Digital signature must be completed before approval.", 409);
        }

        private static bool IsExplicitSignerApproval(ApprovalRequest request)
            => request.FromZone == CdeArea.Shared && request.TargetZone == CdeArea.Published;

        /// <summary>
        /// Sau khi Team Leader approve thành công, file được chuyển sang vùng kế tiếp:
        /// WIP -> Shared, Shared -> Published, Published -> Archived.
        /// File bị reject không đi qua hàm này nên vẫn giữ nguyên folder hiện tại.
        /// </summary>
        private async Task MoveApprovedFileToTargetZoneAsync(FileItem fileItem, Folder currentFolder, CdeArea targetZone, DateTime now)
        {
            var projectFolders = await _zoneResolver.GetProjectFoldersAsync(currentFolder.ProjectId);
            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(
                fileItem,
                currentFolder,
                requireApprovePermission: true);

            var targetFolder = await _zoneResolver.ResolveTargetFolderAsync(
                currentFolder,
                targetZone,
                teamGroupIds,
                projectFolders,
                $"{_zoneResolver.FormatZone(targetZone)} folder not found.");

            fileItem.FolderId = targetFolder.Id;
            fileItem.UpdatedAt = now;
        }

        /// <summary>
        /// Gọi FileVersionService khi file đổi zone thành công:
        /// vào SHARED -> WorkingRevision +1; vào PUBLISHED -> PublishedRevision +1 (C{rev}).
        /// File chưa có version state (chưa upload nội dung) hoặc vào Archived -> không đổi version.
        /// </summary>
        private async Task ApplyZoneVersioningAsync(FileItem fileItem, CdeArea targetZone)
        {
            if (!fileItem.CurrentVersionId.HasValue)
                return;

            var result = targetZone switch
            {
                CdeArea.Shared => await _fileVersionService.GetNextSharedVersionAsync(fileItem.Id),
                CdeArea.Published => await _fileVersionService.GetNextPublishedVersionAsync(fileItem.Id),
                _ => null
            };

            if (result != null)
                fileItem.CurrentVersionId = result.VersionStateId;
        }

        private static CdeArea GetNextApprovalZone(CdeArea currentZone)
            => currentZone switch
            {
                CdeArea.Wip => CdeArea.Shared,
                CdeArea.Shared => CdeArea.Published,
                CdeArea.Published => CdeArea.Archived,
                _ => throw new ApiExceptionResponse("File is already archived and cannot move to next approval zone.", 400)
            };

        #endregion

        #region Xác định team của file

        /// <summary>
        /// Lấy danh sách GroupId của team phụ trách file dựa trên quyền file/folder.
        /// Fallback về toàn bộ group active trong project nếu không có permission nào được cấu hình.
        /// </summary>
        private async Task<IReadOnlyCollection<Guid>> ResolveFileItemTeamGroupIdsAsync(
            FileItem fileItem,
            Folder folder,
            bool requireApprovePermission)
        {
            var activeParticipants = (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(
                    p => p.ProjectId == folder.ProjectId && p.Status == ProjectParticipantStatus.Active))
                .ToDictionary(p => p.Id, p => p.GroupId);
            if (activeParticipants.Count == 0)
                throw new ApiExceptionResponse("File project has no active team.", 400);

            var teamGroupIds = new HashSet<Guid>();

            var filePermissions = await _unitOfWork.Repository<FilePermission>().FindAsync(
                p => p.FileItemId == fileItem.Id
                     && p.ProjectParticipantId.HasValue
                     && (!requireApprovePermission || p.CanApprove));
            foreach (var permission in filePermissions)
            {
                if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
                    teamGroupIds.Add(groupId);
            }

            var allFolders = (await _unitOfWork.Repository<Folder>().FindAsync(
                    f => f.ProjectId == folder.ProjectId))
                .ToDictionary(f => f.Id);

            if (!allFolders.TryGetValue(fileItem.FolderId, out var current))
                throw new ApiExceptionResponse("File folder not found.", 404);

            var folderIds = new HashSet<Guid>();
            while (folderIds.Add(current.Id) && current.ParentFolderId.HasValue
                   && allFolders.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                current = parent;
            }

            //var folderPermissions = await _unitOfWork.Repository<FolderPermissionServiceOld>().FindAsync(
            //    p => folderIds.Contains(p.FolderId)
            //         && p.ProjectParticipantId.HasValue
            //         && (!requireApprovePermission || p.CanApprove));
            //foreach (var permission in folderPermissions)
            //{
            //    if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
            //        teamGroupIds.Add(groupId);
            //}

            return teamGroupIds.Count > 0
                ? teamGroupIds
                : activeParticipants.Values.ToHashSet();
        }

        /// <summary>Overload khi chưa có folder sẵn — tự fetch rồi gọi overload chính.</summary>
        private async Task<IReadOnlyCollection<Guid>> ResolveFileItemTeamGroupIdsAsync(
            FileItem fileItem,
            bool requireApprovePermission)
        {
            var folder = await GetFolderAsync(fileItem.FolderId);
            return await ResolveFileItemTeamGroupIdsAsync(fileItem, folder, requireApprovePermission);
        }

        #endregion

        #region Tạo response

        /// <summary>Lọc danh sách request chỉ giữ lại những request actor có quyền xem.</summary>
        private async Task<IEnumerable<ApprovalRequestResponseDTO>> FilterVisibleRequestsAsync(
            IEnumerable<ApprovalRequest> requests,
            Guid actor)
        {
            var result = new List<ApprovalRequestResponseDTO>();
            var accounts = await GetAccountsByIdAsync();
            var groups = await GetGroupsByIdAsync();

            foreach (var request in requests)
            {
                try
                {
                    var fileItem = await GetFileItemAsync(request.FileItemId);
                    if (await CanViewRequestAsync(actor, request, fileItem))
                    {
                        var folder = await GetFolderAsync(fileItem.FolderId);
                        result.Add(await BuildResponseAsync(request, fileItem, accounts, groups, folder));
                    }
                }
                catch (ApiExceptionResponse ex) when (ex.StatusCode == 404)
                {
                    _logger.LogWarning(
                        ex,
                        "Skipping approval request {ApprovalRequestId} because file item {FileItemId} was not found.",
                        request.Id,
                        request.FileItemId);
                }
            }

            return result;
        }

        private async Task<ApprovalRequestResponseDTO> BuildResponseAsync(ApprovalRequest request, FileItem? fileItem = null)
        {
            fileItem ??= await GetFileItemAsync(request.FileItemId);
            var folder = await GetFolderAsync(fileItem.FolderId);
            var accounts = await GetAccountsByIdAsync();
            var groups = await GetGroupsByIdAsync();
            return await BuildResponseAsync(request, fileItem, accounts, groups, folder);
        }

        private async Task<ApprovalRequestResponseDTO> BuildResponseAsync(
            ApprovalRequest request,
            FileItem fileItem,
            IReadOnlyDictionary<Guid, Account> accounts,
            IReadOnlyDictionary<Guid, Group> groups,
            Folder folder)
        {
            accounts.TryGetValue(request.RequestedBy, out var requester);
            Account? approver = null;
            if (request.ApproverId.HasValue)
                accounts.TryGetValue(request.ApproverId.Value, out approver);

            var signers = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                    s => s.ApprovalRequestId == request.Id))
                .Select(s =>
                {
                    Account? account = null;
                    Group? group = null;
                    if (s.SignerAccountId.HasValue)
                        accounts.TryGetValue(s.SignerAccountId.Value, out account);
                    if (s.SignerGroupId.HasValue)
                        groups.TryGetValue(s.SignerGroupId.Value, out group);

                    return new ApprovalRequestSignerResponseDTO
                    {
                        Id = s.Id,
                        SignerAccountId = s.SignerAccountId,
                        SignerAccountName = account?.UserName,
                        SignerGroupId = s.SignerGroupId,
                        SignerGroupName = group?.Name,
                        Status = s.Status,
                        SignedAt = s.SignedAt,
                        CertificateSerial = s.CertificateSerial
                    };
                })
                .ToList();

            return new ApprovalRequestResponseDTO
            {
                Id = request.Id,
                FileItemId = request.FileItemId,
                FileItemName = fileItem.Name,
                CurrentZone = _zoneResolver.FormatZone(request.FromZone),
                TargetZone = _zoneResolver.FormatZone(request.TargetZone),
                RequiresSignature = request.RequiresSignature,
                IsSigned = request.RequiresSignature
                    ? signers.Count > 0 && signers.All(s => s.Status == ApprovalRequestSignerStatus.Signed)
                    : fileItem.IsSigned,
                Signers = signers,
                RequestedBy = request.RequestedBy,
                RequestedByName = requester?.UserName,
                ApproverId = request.ApproverId,
                ApproverName = approver?.UserName,
                Status = request.Status,
                RejectReason = request.RejectReason,
                CreatedAt = request.CreatedAt,
                ApprovedAt = request.ApprovedAt
            };
        }

        private async Task<Dictionary<Guid, Account>> GetAccountsByIdAsync()
            => (await _unitOfWork.Repository<Account>().GetAllAsync()).ToDictionary(a => a.Id);

        private async Task<Dictionary<Guid, Group>> GetGroupsByIdAsync()
            => (await _unitOfWork.Repository<Group>().GetAllAsync()).ToDictionary(g => g.Id);

        #endregion
    }
}
