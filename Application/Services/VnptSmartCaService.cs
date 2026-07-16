using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.SmartCA;
using Application.DTOs.ResponseDTOs.SmartCA;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Options;
using Application.Services.Signing;
using Domain.Entities;
using Domain.Enum.Account;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Group;
using Domain.Enum.Project;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    /// <summary>
    /// Xu ly nghiep vu ky so VNPT SmartCA cho approval request cua file CDE.
    /// Service chi tao giao dich ky, kiem tra trang thai va luu thong tin ky;
    /// viec approve file van nam trong ApprovalService.
    /// </summary>
    public class VnptSmartCaService : IVnptSmartCaService
    {
        private const string GetCertificatePath = "v1/credentials/get_certificate";
        private const string SignPath = "v1/signatures/sign";
        private static readonly TimeSpan WaitingConfirmTimeout = TimeSpan.FromMinutes(5);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPdfSignatureService _pdfSignatureService;
        private readonly IApprovalService _approvalService;
        private readonly IFileStorageService _storage;
        private readonly INotificationService _notification;
        private readonly VnptSmartCaOptions _options;
        private readonly ILogger<VnptSmartCaService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public VnptSmartCaService(
            IUnitOfWork unitOfWork,
            IHttpClientFactory httpClientFactory,
            IPdfSignatureService pdfSignatureService,
            IApprovalService approvalService,
            IFileStorageService storage,
            INotificationService notification,
            IOptions<VnptSmartCaOptions> options,
            ILogger<VnptSmartCaService> logger)
        {
            _unitOfWork = unitOfWork;
            _httpClientFactory = httpClientFactory;
            _pdfSignatureService = pdfSignatureService;
            _approvalService = approvalService;
            _storage = storage;
            _notification = notification;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Lay danh sach chung thu so cua user tren VNPT SmartCA.
        /// </summary>
        public async Task<ApiResponse> GetCertificatesAsync(
            Guid approvalId,
            GetCertificateRequestDto request,
            Guid currentUserId)
        {
            var validation = await ValidateSigningContextAsync(approvalId, currentUserId, blockSignedFile: true);
            if (validation.Error != null)
                return ApiResponse.Fail(validation.Error);

            if (string.IsNullOrWhiteSpace(request.UserId))
                return ApiResponse.Fail("VNPT SmartCA user id is required.");

            var (certificates, external) = await FetchCertificatesFromVnptAsync(request.UserId, request.SerialNumber);
            if (certificates == null)
                return BuildExternalFailResponse(external!);

            return ApiResponse.Success("Certificates retrieved", certificates);
        }

        /// <summary>
        /// Goi lai API get_certificate cua VNPT (dung chung logic voi GetCertificatesAsync) de lay
        /// certificate bytes (DER base64) cua nguoi ky truoc khi chuan bi PDF ky 2 pha. Tra ve null neu
        /// khong lay duoc DER bytes (fail som, ro rang, thay vi de PDF ky xong roi moi phat hien thieu cert).
        /// </summary>
        private async Task<CertificateDto?> FetchSignerCertificateAsync(string userId, string serialNumber)
        {
            var (certificates, _) = await FetchCertificatesFromVnptAsync(userId, serialNumber);
            return certificates?.FirstOrDefault(c =>
                string.Equals(c.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(c.CertificateDataBase64))
                ?? certificates?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.CertificateDataBase64));
        }

        private async Task<(IReadOnlyCollection<CertificateDto>? Certificates, ExternalSmartCaResponse? External)> FetchCertificatesFromVnptAsync(
            string userId,
            string? serialNumber)
        {
            var payload = new
            {
                sp_id = _options.SpId,
                sp_password = _options.SpPassword,
                user_id = userId,
                serial_number = serialNumber,
                transaction_id = GenerateSmartCaTransactionId()
            };

            var external = await PostJsonAsync(GetCertificatePath, payload);
            if (!external.IsBusinessSuccess)
                return (null, external);

            return (ParseCertificates(external.RawResponse), null);
        }

        /// <summary>
        /// Tao giao dich ky so tren VNPT SmartCA va luu transaction voi trang thai WaitingConfirm.
        /// </summary>
        public async Task<ApiResponse> SendSignRequestAsync(
            Guid approvalId,
            SendSignRequestDto request,
            Guid currentUserId)
        {
            var validation = await ValidateSigningContextAsync(approvalId, currentUserId, blockSignedFile: true);
            if (validation.Error != null)
                return ApiResponse.Fail(validation.Error);

            if (string.IsNullOrWhiteSpace(request.UserId)
                || string.IsNullOrWhiteSpace(request.CertificateSerial))
            {
                return ApiResponse.Fail("UserId and certificate serial are required.");
            }

            var pendingTransactions = (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == approvalId
                         && t.SignedBy == currentUserId
                         && t.Status == SignatureTransactionStatus.WaitingConfirm))
                .ToList();
            var staleCutoff = DateTime.UtcNow - WaitingConfirmTimeout;
            var stillPending = false;
            foreach (var pending in pendingTransactions)
            {
                if (pending.CreatedAt <= staleCutoff)
                {
                    pending.Status = SignatureTransactionStatus.Expired;
                    pending.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    stillPending = true;
                }
            }
            if (pendingTransactions.Any(t => t.Status == SignatureTransactionStatus.Expired))
                await _unitOfWork.CommitAsync();
            if (stillPending)
                return ApiResponse.Fail("A signing transaction is already waiting for confirmation.");

            var context = validation.Context!;
            var transactionId = GenerateSmartCaTransactionId();
            var fileType = Path.GetExtension(context.FileItem.Name).TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(fileType)) fileType = "pdf";

            var signerCertificate = await FetchSignerCertificateAsync(request.UserId, request.CertificateSerial);
            if (signerCertificate == null || string.IsNullOrWhiteSpace(signerCertificate.CertificateDataBase64))
            {
                return ApiResponse.Fail(
                    "Could not retrieve signer certificate data from VNPT SmartCA (required to embed a real digital signature).");
            }
            var signerCertDer = Convert.FromBase64String(signerCertificate.CertificateDataBase64);

            PdfExternalSignatureHelper.PreparedSignature prepared;
            try
            {
                prepared = await _pdfSignatureService.PrepareSignatureAsync(
                    approvalId, currentUserId, request.CertificateSerial, signerCertDer, transactionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prepare PDF for external signature (approval {ApprovalId})", approvalId);
                return ApiResponse.Fail($"Failed to prepare file for signing: {ex.Message}");
            }

            using var preparedStream = new MemoryStream(prepared.PreparedPdfBytes);
            var storedPrepared = await _storage.SaveAsync(preparedStream, context.Folder.ProjectId, context.FileItem.FolderId, ".pdf");

            var dataToBeSigned = Convert.ToHexString(prepared.HashToSign).ToLowerInvariant();

            var payload = new
            {
                sp_id = _options.SpId,
                sp_password = _options.SpPassword,
                user_id = request.UserId,
                serial_number = request.CertificateSerial,
                transaction_id = transactionId,
                sign_files = new[]
                {
                    new
                    {
                        doc_id = context.FileItem.Id.ToString(),
                        file_type = fileType,
                        sign_type = "hash",
                        data_to_be_signed = dataToBeSigned
                    }
                }
            };

            var external = await PostJsonAsync(SignPath, payload);
            if (!external.IsBusinessSuccess)
                return BuildExternalFailResponse(external);

            var now = DateTime.UtcNow;
            var transaction = new ApprovalSignatureTransaction
            {
                Id = Guid.NewGuid(),
                ApprovalRequestId = context.ApprovalRequest.Id,
                FileItemId = context.FileItem.Id,
                TransactionId = transactionId,
                CertificateSerial = request.CertificateSerial,
                Sad = ExtractString(external.RawResponse, "sad", "SAD"),
                SignedBy = currentUserId,
                Status = SignatureTransactionStatus.WaitingConfirm,
                CreatedAt = now,
                UpdatedAt = now,
                RawRequest = external.SafeRawRequest,
                RawResponse = external.RawResponse,
                PreparedPdfStoragePath = storedPrepared.RelativePath,
                DigestBase64 = Convert.ToBase64String(prepared.DocumentDigest),
                SignedAttributesBase64 = Convert.ToBase64String(prepared.SignedAttributes),
                SignerCertificateBase64 = Convert.ToBase64String(signerCertDer),
                HashAlgorithm = "SHA-256"
            };

            await _unitOfWork.Repository<ApprovalSignatureTransaction>().CreateAsync(transaction);
            await _unitOfWork.CommitAsync();

            return ApiResponse.Success("Sign request created successfully", new SendSignResponseDto
            {
                ApprovalRequestId = transaction.ApprovalRequestId,
                FileItemId = transaction.FileItemId,
                TransactionId = transaction.TransactionId,
                Sad = transaction.Sad,
                Status = transaction.Status,
                Message = external.Message
            });
        }

        /// <summary>
        /// Kiem tra trang thai giao dich ky tren VNPT, cap nhat file thanh da ky neu VNPT tra chu ky hop le.
        /// </summary>
        public async Task<ApiResponse> GetTransactionStatusAsync(
            Guid approvalId,
            string transactionId,
            Guid currentUserId)
        {
            var validation = await ValidateSigningContextAsync(approvalId, currentUserId, blockSignedFile: false);
            if (validation.Error != null)
                return ApiResponse.Fail(validation.Error);
            var transaction = await GetTransactionAsync(approvalId, transactionId, currentUserId);
            if (transaction == null)
                return ApiResponse.Fail("Signature transaction not found.");

            var payload = new
            {
                sp_id = _options.SpId,
                sp_password = _options.SpPassword,
                transaction_id = transactionId
            };

            var external = await PostJsonAsync($"v1/signatures/sign/{Uri.EscapeDataString(transactionId)}/status", payload);
            if (!external.HttpSucceeded)
                return BuildExternalFailResponse(external);

            var now = DateTime.UtcNow;
            transaction.RawResponse = external.RawResponse;
            transaction.UpdatedAt = now;

            var signatureValue = ExtractSignatureValue(external.RawResponse);
            if (!string.IsNullOrWhiteSpace(signatureValue))
                transaction.SignatureValueBase64 = signatureValue;
            var newStatus = signatureValue != null
                ? SignatureTransactionStatus.Signed
                : ExtractStatus(external.RawResponse);

            // App SmartCA co the loi ngay o buoc ket noi module ky tren dien thoai (truoc khi nguoi dung
            // kip Xac nhan/Tu choi) -> VNPT khong bao gio tra ve trang thai khac WaitingConfirm. Qua thoi
            // gian hop ly thi tu coi la Expired de nguoi dung duoc bao va tao yeu cau ky lai duoc.
            if (newStatus is null or SignatureTransactionStatus.Created or SignatureTransactionStatus.WaitingConfirm
                && now - transaction.CreatedAt > WaitingConfirmTimeout)
            {
                newStatus = SignatureTransactionStatus.Expired;
            }

            var justSigned = false;

            if (newStatus is SignatureTransactionStatus.Signed
                or SignatureTransactionStatus.Failed
                or SignatureTransactionStatus.Expired)
            {
                transaction.Status = newStatus.Value;
            }

            if (transaction.Status == SignatureTransactionStatus.Signed
                && validation.Context!.Signer.Status != ApprovalRequestSignerStatus.Signed)
            {
                transaction.SignedAt ??= now;
                transaction.SignedBy ??= currentUserId;
                validation.Context.Signer.Status = ApprovalRequestSignerStatus.Signed;
                validation.Context.Signer.SignedAt = now;
                validation.Context.Signer.CertificateSerial = transaction.CertificateSerial;
                await CompleteImplicitSignersAsync(validation.Context.ApprovalRequest, transaction.CertificateSerial, now);
                justSigned = true;
            }

            await _unitOfWork.CommitAsync();
            var message = "Transaction status retrieved";
            if (justSigned)
            {
                if (await AreRequiredSignersCompleteAsync(validation.Context!.ApprovalRequest))
                {
                    message = await CompleteSignedFileAndApproveAsync(validation.Context!, currentUserId);
                }
                else
                {
                    message = "Signer signed successfully. Waiting for remaining required signers.";
                    await NotifyRemainingSignersAsync(approvalId, validation.Context!.ApprovalRequest, validation.Context.FileItem);
                }
            }
            else if (transaction.Status == SignatureTransactionStatus.Signed
                     && validation.Context!.ApprovalRequest.Status == ApprovalRequestStatus.Pending
                     && await AreRequiredSignersCompleteAsync(validation.Context.ApprovalRequest))
            {
                message = await CompleteSignedFileAndApproveAsync(validation.Context, currentUserId);
            }

            return ApiResponse.Success(message, new TransactionStatusDto
            {
                TransactionId = transaction.TransactionId,
                Status = transaction.Status
            });
        }

        /// <summary>
        /// Lay thong tin giao dich ky moi nhat cua approval request da luu trong he thong.
        /// </summary>
        public async Task<ApiResponse> GetApprovalSignatureAsync(
            Guid approvalId,
            Guid currentUserId)
        {
            var validation = await ValidateSigningContextAsync(approvalId, currentUserId, blockSignedFile: false);
            if (validation.Error != null)
                return ApiResponse.Fail(validation.Error);

            var transaction = (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == approvalId))
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();
            if (transaction == null)
                return ApiResponse.Fail("Signature transaction not found.");

            return ApiResponse.Success("Signature info retrieved", MapSignatureInfo(transaction));
        }

        /// <summary>
        /// Kiem tra dieu kien chung truoc khi thao tac SmartCA:
        /// approval/file ton tai, user la Team Leader active, file dang pending va yeu cau ky so.
        /// </summary>
        private async Task<SigningValidationResult> ValidateSigningContextAsync(
            Guid approvalId,
            Guid currentUserId,
            bool blockSignedFile)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl)
                || string.IsNullOrWhiteSpace(_options.SpId)
                || string.IsNullOrWhiteSpace(_options.SpPassword))
            {
                return SigningValidationResult.Fail("VNPT SmartCA configuration is missing.");
            }

            var approval = await _unitOfWork.Repository<ApprovalRequest>().GetByIdAsync(approvalId);
            if (approval == null)
                return SigningValidationResult.Fail("Approval request not found.");

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(approval.FileItemId);
            if (fileItem == null)
                return SigningValidationResult.Fail("File not found.");

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);
            if (folder == null)
                return SigningValidationResult.Fail("File folder not found.");

            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(currentUserId);
            if (account == null || account.Status != AccountStatus.Active)
                return SigningValidationResult.Fail("Current user must be active.");

            if (approval.Status != ApprovalRequestStatus.Pending)
                return SigningValidationResult.Fail("Approval request must be pending.");

            if (fileItem.Status != FileItemStatus.PendingApproval)
                return SigningValidationResult.Fail("File must be pending approval.");

            if (!approval.RequiresSignature)
                return SigningValidationResult.Fail("This file does not require digital signature.");

            var signer = await ResolveSignerAsync(approval.Id, currentUserId);
            if (signer == null)
                return SigningValidationResult.Fail("Current user is not required to sign this approval request.");

            if (blockSignedFile && signer.Status == ApprovalRequestSignerStatus.Signed)
                return SigningValidationResult.Fail("Current user has already signed this approval request.");

            return SigningValidationResult.Success(new SigningContext(approval, fileItem, folder, signer));
        }

        private async Task<ApprovalRequestSigner?> ResolveSignerAsync(Guid approvalId, Guid currentUserId)
        {
            var signers = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                    s => s.ApprovalRequestId == approvalId))
                .ToList();

            var accountSigner = signers.FirstOrDefault(s => s.SignerAccountId == currentUserId);
            if (accountSigner != null)
                return accountSigner;

            var groupIds = signers
                .Where(s => s.SignerGroupId.HasValue)
                .Select(s => s.SignerGroupId!.Value)
                .ToHashSet();
            if (groupIds.Count == 0)
                return null;

            var activeGroupIds = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => m.AccountId == currentUserId
                         && groupIds.Contains(m.GroupId)
                         && m.Status == GroupMemberStatus.Active))
                .Select(m => m.GroupId)
                .ToHashSet();

            return signers.FirstOrDefault(s => s.SignerGroupId.HasValue && activeGroupIds.Contains(s.SignerGroupId.Value));
        }

        /// <summary>
        /// Bao cho cac signer con lai (chua ky) rang toi luot ho - ap dung khi approval can nhieu nguoi
        /// ky (explicit signer) va 1 nguoi vua ky xong nhung chua du. Voi signer dang group (khong chi
        /// dinh tai khoan cu the), bao cho toan bo active member cua group do (ai trong group cung ky
        /// duoc, giong dung co che ResolveSignerAsync).
        /// </summary>
        private async Task NotifyRemainingSignersAsync(Guid approvalId, ApprovalRequest approval, FileItem fileItem)
        {
            var pendingSigners = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                    s => s.ApprovalRequestId == approval.Id && s.Status != ApprovalRequestSignerStatus.Signed))
                .ToList();
            if (pendingSigners.Count == 0)
                return;

            var accountIds = pendingSigners
                .Where(s => s.SignerAccountId.HasValue)
                .Select(s => s.SignerAccountId!.Value)
                .ToHashSet();

            var pendingGroupIds = pendingSigners
                .Where(s => s.SignerGroupId.HasValue)
                .Select(s => s.SignerGroupId!.Value)
                .ToHashSet();
            if (pendingGroupIds.Count > 0)
            {
                var groupMemberIds = (await _unitOfWork.Repository<GroupMember>().FindAsync(
                        m => pendingGroupIds.Contains(m.GroupId) && m.Status == GroupMemberStatus.Active))
                    .Select(m => m.AccountId);
                accountIds.UnionWith(groupMemberIds);
            }

            if (accountIds.Count == 0)
                return;

            await _notification.NotifyManyAsync(
                accountIds,
                $"\"{fileItem.Name}\" đang chờ bạn ký số.",
                linkType: "Approval",
                linkId: approvalId.ToString());
        }

        private async Task<bool> AreRequiredSignersCompleteAsync(ApprovalRequest approval)
        {
            var signers = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                    s => s.ApprovalRequestId == approval.Id))
                .ToList();

            return IsExplicitSignerApproval(approval)
                ? signers.Count > 0 && signers.All(s => s.Status == ApprovalRequestSignerStatus.Signed)
                : signers.Any(s => s.Status == ApprovalRequestSignerStatus.Signed);
        }

        private async Task CompleteImplicitSignersAsync(ApprovalRequest approval, string? certificateSerial, DateTime signedAt)
        {
            if (IsExplicitSignerApproval(approval))
                return;

            var signers = await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                s => s.ApprovalRequestId == approval.Id);
            foreach (var signer in signers.Where(s => s.Status != ApprovalRequestSignerStatus.Signed))
            {
                signer.Status = ApprovalRequestSignerStatus.Signed;
                signer.SignedAt = signedAt;
                signer.CertificateSerial = certificateSerial;
            }
        }

        private static bool IsExplicitSignerApproval(ApprovalRequest approval)
            => approval.FromZone == CdeArea.Shared && approval.TargetZone == CdeArea.Published;

        private async Task<string> CompleteSignedFileAndApproveAsync(SigningContext context, Guid currentUserId)
        {
            if (context.FileItem.FileType == FileType.Pdf || await IsSignableFileAsync(context.FileItem))
            {
                var signedFileResult = await _pdfSignatureService.GenerateSignedPdfAsync(context.ApprovalRequest.Id, currentUserId);
                if (!signedFileResult.IsSuccess)
                    return signedFileResult.Message;
            }
            else if (!context.FileItem.IsSigned)
            {
                context.FileItem.IsSigned = true;
                context.FileItem.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.CommitAsync();
            }

            await _approvalService.ApproveAsync(context.ApprovalRequest.Id, currentUserId);
            return "Signed file generated successfully and approval completed.";
        }

        // Office (Word/Excel) hoac ban ve CAD 2D (dwg/dwgx, convert qua ConvertAPI - xem PdfSignatureService)
        // deu duoc stamp de len ban PDF da chuyen doi truoc khi approve.
        private async Task<bool> IsSignableFileAsync(FileItem fileItem)
        {
            if (fileItem.FileType != FileType.Office && fileItem.FileType != FileType.Cad)
                return false;
            if (!fileItem.CurrentVersionId.HasValue)
                return false;

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value);
            var format = (version?.Format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            return format is "doc" or "docx" or "xls" or "xlsx" or "dwg" or "dwgx";
        }

        /// <summary>
        /// Lay transaction ky theo approval va transaction id, CHI cua chinh currentUserId (nguoi da gui
        /// yeu cau ky nay) -> tranh mot signer lay/doan trung transactionId cua signer khac roi bi
        /// danh dau Signed "ho" du chua tung ky.
        private async Task<ApprovalSignatureTransaction?> GetTransactionAsync(Guid approvalId, string transactionId, Guid currentUserId)
            => (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == approvalId
                         && t.TransactionId == transactionId
                         && t.SignedBy == currentUserId))
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();

        /// <summary>
        /// Goi API VNPT SmartCA bang JSON va tra ve response gom HTTP status, raw response va message da parse.
        /// </summary>
        private async Task<ExternalSmartCaResponse> PostJsonAsync(string path, object payload)
        {
            var rawRequest = JsonSerializer.Serialize(payload, JsonOptions);
            var safeRawRequest = SanitizeRawRequest(rawRequest);
            try
            {
                using var content = new StringContent(rawRequest, Encoding.UTF8, "application/json");
                using var response = await CreateClient().PostAsync(path, content);
                var rawResponse = await response.Content.ReadAsStringAsync();
                var httpSucceeded = response.IsSuccessStatusCode;
                var businessSucceeded = httpSucceeded && IsExternalBusinessSuccess(rawResponse);
                var message = ExtractString(
                                  rawResponse,
                                  "message",
                                  "Message",
                                  "error",
                                  "Error",
                                  "errorMessage",
                                  "error_description",
                                  "description",
                                  "desc",
                                  "mess")
                              ?? (businessSucceeded ? "VNPT SmartCA request succeeded." : "VNPT SmartCA request failed.");

                return new ExternalSmartCaResponse(
                    rawRequest,
                    safeRawRequest,
                    rawResponse,
                    (int)response.StatusCode,
                    httpSucceeded,
                    businessSucceeded,
                    message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VNPT SmartCA request failed at {Path}", path);
                return new ExternalSmartCaResponse(
                    rawRequest,
                    safeRawRequest,
                    string.Empty,
                    0,
                    false,
                    false,
                    "Cannot connect to VNPT SmartCA service.");
            }
        }

        private static ApiResponse BuildExternalFailResponse(ExternalSmartCaResponse external)
            => ApiResponse.Fail(external.Message, new
            {
                external.StatusCode,
                external.HttpSucceeded,
                external.RawResponse
            });

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            return client;
        }

        private static string GenerateSmartCaTransactionId()
            => $"SP_CA_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        /// <summary>
        /// Parse danh sach chung thu so tu response VNPT, bao gom response bi boc nhieu lop responseBody/data.
        /// </summary>
        private static IReadOnlyCollection<CertificateDto> ParseCertificates(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                return Array.Empty<CertificateDto>();

            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                var root = document.RootElement;
                var source = ResolveCertificateSource(root);

                if (source.ValueKind == JsonValueKind.Array)
                    return source.EnumerateArray().Select(MapCertificate).ToList();

                if (source.ValueKind == JsonValueKind.Object)
                    return new[] { MapCertificate(source) };
            }
            catch (JsonException)
            {
                return Array.Empty<CertificateDto>();
            }

            return Array.Empty<CertificateDto>();
        }

        private static JsonElement ResolveCertificateSource(JsonElement root)
        {
            var current = root;
            while (current.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(current, out var certificates, "user_certificates", "userCertificates", "certificates", "Certificates"))
                    return certificates;

                if (TryGetProperty(current, out var wrapper, "responseBody", "data", "Data", "result", "Result"))
                {
                    current = wrapper;
                    continue;
                }

                break;
            }

            return current.ValueKind == JsonValueKind.Array ? current : default;
        }

        /// <summary>
        /// Map mot phan tu certificate cua VNPT sang DTO noi bo.
        /// </summary>
        private static CertificateDto MapCertificate(JsonElement element)
            => new()
            {
                SerialNumber = GetString(
                    element,
                    "serial_number",
                    "serialNumber",
                    "SerialNumber",
                    "serial",
                    "cert_serial",
                    "certificate_serial",
                    "certificateSerial") ?? string.Empty,
                Subject = GetString(element, "cert_subject", "subject", "Subject", "subjectDN", "subject_dn"),
                Issuer = GetString(element, "issuer", "Issuer", "issuerDN", "issuer_dn"),
                ValidFrom = GetDateTime(element, "cert_valid_from", "validFrom", "ValidFrom", "valid_from", "notBefore", "not_before"),
                ValidTo = GetDateTime(element, "cert_valid_to", "validTo", "ValidTo", "valid_to", "notAfter", "not_after"),
                Status = GetString(element, "cert_status_code", "cert_status", "status", "Status"),
                CertificateDataBase64 = GetString(
                    element,
                    "cert_data",
                    "certData",
                    "certificate",
                    "certificate_data",
                    "cert_value",
                    "certValue",
                    "x509_certificate",
                    "x509Certificate",
                    "signing_certificate",
                    "user_certificate"),
                ChainCertificateDataBase64 = GetStringArray(element, "cert_chain", "certChain", "chain", "ca_chain", "caChain")
            };

        /// <summary>Lay mang chuoi tu 1 trong cac ten field co the co (VNPT co the tra chain duoi dang array).</summary>
        private static IReadOnlyList<string>? GetStringArray(JsonElement element, params string[] names)
        {
            if (!TryFindProperty(element, out var value, names) || value.ValueKind != JsonValueKind.Array)
                return null;

            var result = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        result.Add(text);
                }
            }
            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Xac dinh thanh cong nghiep vu tu body VNPT, khong chi dua vao HTTP status.
        /// </summary>
        private static bool IsExternalBusinessSuccess(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                return true;

            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                var root = UnwrapSmartCaResponse(document.RootElement);

                if (TryGetBool(root, out var success, "success", "Success", "isSuccess", "IsSuccess", "is_success"))
                    return success;

                if (TryGetInt(root, out var statusCode, "status_code", "statusCode", "StatusCode"))
                    return statusCode is >= 200 and < 300;

                if (TryGetString(root, out var status, "status", "Status", "transaction_status"))
                {
                    var normalized = Normalize(status);
                    if (normalized is "failed" or "fail" or "error" or "expired" or "rejected")
                        return false;
                }
            }
            catch (JsonException)
            {
                return true;
            }

            return true;
        }

        /// <summary>
        /// Parse trang thai giao dich ky tu raw response VNPT.
        /// </summary>
        private static SignatureTransactionStatus? ExtractStatus(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                return null;

            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                var root = UnwrapSmartCaResponse(document.RootElement);

                if (TryFindProperty(root, out var statusElement, "status", "Status", "transaction_status"))
                {
                    if (statusElement.ValueKind == JsonValueKind.Number
                        && statusElement.TryGetInt32(out var statusNumber)
                        && Enum.IsDefined(typeof(SignatureTransactionStatus), statusNumber))
                    {
                        return (SignatureTransactionStatus)statusNumber;
                    }

                    if (statusElement.ValueKind == JsonValueKind.String)
                    {
                        var fromStatus = Normalize(statusElement.GetString()) switch
                        {
                            "created" => SignatureTransactionStatus.Created,
                            "waitingconfirm" or "waiting" or "pending" => SignatureTransactionStatus.WaitingConfirm,
                            "signed" or "success" or "completed" => SignatureTransactionStatus.Signed,
                            "failed" or "fail" or "error" or "rejected" => SignatureTransactionStatus.Failed,
                            "expired" => SignatureTransactionStatus.Expired,
                            _ => (SignatureTransactionStatus?)null
                        };
                        if (fromStatus != null)
                            return fromStatus;
                    }
                }

                // VNPT bao nguoi ky tu choi tren dien thoai qua field "message" (vd {"message":"REJECTED"}),
                // KHONG co field "status" rieng trong truong hop nay -> phai kiem tra ca message.
                if (TryFindProperty(root, out var messageElement, "message", "Message")
                    && messageElement.ValueKind == JsonValueKind.String)
                {
                    var rejected = Normalize(messageElement.GetString()) switch
                    {
                        "rejected" or "reject" or "denied" or "deny" or "declined" or "decline"
                            or "cancelled" or "canceled" or "cancel" or "usercancelled" or "userrejected" => SignatureTransactionStatus.Failed,
                        _ => (SignatureTransactionStatus?)null
                    };
                    if (rejected != null)
                        return rejected;
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Lay gia tri chu ky tu response status cua VNPT neu da ky thanh cong.
        /// </summary>
        private static string? ExtractSignatureValue(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                return null;

            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                var root = UnwrapSmartCaResponse(document.RootElement);

                if (TryFindProperty(root, out var signatures, "signatures")
                    && signatures.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sig in signatures.EnumerateArray())
                    {
                        var value = GetString(sig, "signature_value", "signatureValue");
                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Tim chuoi theo danh sach ten field trong response VNPT.
        /// </summary>
        private static string? ExtractString(string rawResponse, params string[] names)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                return null;

            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                return GetString(UnwrapSmartCaResponse(document.RootElement), names);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? GetString(JsonElement element, params string[] names)
        {
            if (!TryFindProperty(element, out var value, names))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static DateTime? GetDateTime(JsonElement element, params string[] names)
        {
            var value = GetString(element, names);
            return DateTime.TryParse(value, out var result) ? result : null;
        }

        private static bool TryGetString(JsonElement element, out string value, params string[] names)
        {
            value = string.Empty;
            var raw = GetString(element, names);
            if (raw == null)
                return false;

            value = raw;
            return true;
        }

        private static bool TryGetBool(JsonElement element, out bool value, params string[] names)
        {
            value = false;
            if (!TryFindProperty(element, out var property, names))
                return false;

            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }

            return property.ValueKind == JsonValueKind.String
                   && bool.TryParse(property.GetString(), out value);
        }

        private static bool TryGetInt(JsonElement element, out int value, params string[] names)
        {
            value = 0;
            if (!TryFindProperty(element, out var property, names))
                return false;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
                return true;

            return property.ValueKind == JsonValueKind.String
                   && int.TryParse(property.GetString(), out value);
        }

        private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
        {
            value = default;
            if (element.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out value))
                    return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindProperty(JsonElement element, out JsonElement value, params string[] names)
        {
            if (TryGetProperty(element, out value, names))
                return true;

            if (element.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object
                    && TryFindProperty(property.Value, out value, names))
                {
                    return true;
                }
            }

            return false;
        }

        private static JsonElement UnwrapSmartCaResponse(JsonElement root)
        {
            var current = root;
            while (current.ValueKind == JsonValueKind.Object
                   && TryGetProperty(current, out var inner, "responseBody"))
            {
                current = inner;
            }

            return current;
        }

        /// <summary>
        /// An cac gia tri nhay cam truoc khi luu raw request vao database.
        /// </summary>
        private static string SanitizeRawRequest(string rawRequest)
        {
            if (string.IsNullOrWhiteSpace(rawRequest))
                return rawRequest;

            try
            {
                using var document = JsonDocument.Parse(rawRequest);
                var sanitized = SanitizeElement(document.RootElement);
                return JsonSerializer.Serialize(sanitized, JsonOptions);
            }
            catch (JsonException)
            {
                return rawRequest;
            }
        }

        private static object? SanitizeElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => IsSensitiveField(property.Name)
                        ? "***"
                        : SanitizeElement(property.Value)),
                JsonValueKind.Array => element.EnumerateArray().Select(SanitizeElement).ToList(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.GetRawText()
            };
        }

        private static bool IsSensitiveField(string fieldName)
            => fieldName.Equals("sp_password", StringComparison.OrdinalIgnoreCase)
               || fieldName.Equals("password", StringComparison.OrdinalIgnoreCase)
               || fieldName.Equals("otp", StringComparison.OrdinalIgnoreCase)
               || fieldName.Equals("sad", StringComparison.OrdinalIgnoreCase);

        private static string Normalize(string? value)
            => new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

        private static SignatureInfoDto MapSignatureInfo(ApprovalSignatureTransaction transaction)
            => new()
            {
                ApprovalRequestId = transaction.ApprovalRequestId,
                FileItemId = transaction.FileItemId,
                TransactionId = transaction.TransactionId,
                CertificateSerial = transaction.CertificateSerial,
                SignedBy = transaction.SignedBy,
                SignedAt = transaction.SignedAt,
                Status = transaction.Status
            };

        /// <summary>Ngu canh da xac thuc cua 1 thao tac ky SmartCA: approval/file/folder/signer lien quan.</summary>
        private sealed record SigningContext(
            ApprovalRequest ApprovalRequest,
            FileItem FileItem,
            Folder Folder,
            ApprovalRequestSigner Signer);

        /// <summary>Ket qua xac thuc ngu canh ky: co Context (thanh cong) hoac Error (that bai), khong bao gio ca hai.</summary>
        private sealed record SigningValidationResult(SigningContext? Context, string? Error)
        {
            public static SigningValidationResult Success(SigningContext context) => new(context, null);
            public static SigningValidationResult Fail(string error) => new(null, error);
        }

        /// <summary>Response tho tu goi API VNPT SmartCA, da parse san HTTP status va business success.</summary>
        private sealed record ExternalSmartCaResponse(
            string RawRequest,
            string SafeRawRequest,
            string RawResponse,
            int StatusCode,
            bool HttpSucceeded,
            bool IsBusinessSuccess,
            string Message);
    }
}
