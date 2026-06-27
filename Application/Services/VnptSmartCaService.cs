using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.SmartCA;
using Application.DTOs.ResponseDTOs.SmartCA;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Options;
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

        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPdfSignatureService _pdfSignatureService;
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
            IOptions<VnptSmartCaOptions> options,
            ILogger<VnptSmartCaService> logger)
        {
            _unitOfWork = unitOfWork;
            _httpClientFactory = httpClientFactory;
            _pdfSignatureService = pdfSignatureService;
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

            var payload = new
            {
                sp_id = _options.SpId,
                sp_password = _options.SpPassword,
                user_id = request.UserId,
                serial_number = request.SerialNumber,
                transaction_id = GenerateSmartCaTransactionId()
            };

            var external = await PostJsonAsync(GetCertificatePath, payload);
            if (!external.IsBusinessSuccess)
                return BuildExternalFailResponse(external);

            var certificates = ParseCertificates(external.RawResponse);
            return ApiResponse.Success("Certificates retrieved", certificates);
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

            var hasPendingTransaction = (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == approvalId
                         && t.Status == SignatureTransactionStatus.WaitingConfirm))
                .Any();
            if (hasPendingTransaction)
                return ApiResponse.Fail("A signing transaction is already waiting for confirmation.");

            var context = validation.Context!;
            var transactionId = GenerateSmartCaTransactionId();
            var dataToBeSigned = ComputeDataToBeSigned(context.FileItem, context.ApprovalRequest, transactionId);
            var fileType = Path.GetExtension(context.FileItem.Name).TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(fileType)) fileType = "pdf";

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
                Status = SignatureTransactionStatus.WaitingConfirm,
                CreatedAt = now,
                UpdatedAt = now,
                RawRequest = external.SafeRawRequest,
                RawResponse = external.RawResponse
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

            var transaction = await GetTransactionAsync(approvalId, transactionId);
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
            var justSigned = false;
            if (signatureValue != null && transaction.Status != SignatureTransactionStatus.Signed)
            {
                transaction.Status = SignatureTransactionStatus.Signed;
                transaction.SignedAt = now;
                transaction.SignedBy = currentUserId;
                justSigned = true;
            }
            else
            {
                var newStatus = ExtractStatus(external.RawResponse);
                if (newStatus is SignatureTransactionStatus.Signed
                    or SignatureTransactionStatus.Failed
                    or SignatureTransactionStatus.Expired)
                {
                    transaction.Status = newStatus.Value;
                }
            }

            await _unitOfWork.CommitAsync();

            // Sau khi VNPT bao da ky, sinh ban PDF da stamp chu ky truc quan.
            // FileItem.IsSigned chi duoc danh dau true trong PdfSignatureService, sau khi PDF sinh thanh cong.
            var message = "Transaction status retrieved";
            if (justSigned)
            {
                var pdfResult = await _pdfSignatureService.GenerateSignedPdfAsync(approvalId, currentUserId);
                if (!pdfResult.IsSuccess)
                    message = "SmartCA signed successfully but signed PDF generation failed.";
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

            var teamGroupIds = await ResolveFileItemTeamGroupIdsAsync(fileItem, folder, requireApprovePermission: true);
            if (!await IsActiveTeamLeaderAsync(currentUserId, teamGroupIds))
                return SigningValidationResult.Fail("Current user must be active Team Leader.");

            if (approval.Status != ApprovalRequestStatus.Pending)
                return SigningValidationResult.Fail("Approval request must be pending.");

            if (fileItem.Status != FileItemStatus.PendingApproval)
                return SigningValidationResult.Fail("File must be pending approval.");

            if (folder.Area != CdeArea.Wip)
                return SigningValidationResult.Fail("File must be in WIP zone.");

            if (!fileItem.RequiresSignature)
                return SigningValidationResult.Fail("This file does not require digital signature.");

            if (blockSignedFile && fileItem.IsSigned)
                return SigningValidationResult.Fail("This file has already been signed.");

            return SigningValidationResult.Success(new SigningContext(approval, fileItem, folder));
        }

        /// <summary>
        /// Xac dinh cac group phu trach file dua tren permission cua file va folder cha.
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
                return Array.Empty<Guid>();

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

            var folders = (await _unitOfWork.Repository<Folder>().FindAsync(
                    f => f.ProjectId == folder.ProjectId))
                .ToList();
            var byId = folders.ToDictionary(f => f.Id);

            if (!byId.TryGetValue(fileItem.FolderId, out var current))
                return teamGroupIds;

            var folderIds = new HashSet<Guid>();
            while (folderIds.Add(current.Id) && current.ParentFolderId.HasValue
                   && byId.TryGetValue(current.ParentFolderId.Value, out var parent))
            {
                current = parent;
            }

            var folderPermissions = await _unitOfWork.Repository<FolderPermission>().FindAsync(
                p => folderIds.Contains(p.FolderId)
                     && p.ProjectParticipantId.HasValue
                     && (!requireApprovePermission || p.CanApprove));
            foreach (var permission in folderPermissions)
            {
                if (activeParticipants.TryGetValue(permission.ProjectParticipantId!.Value, out var groupId))
                    teamGroupIds.Add(groupId);
            }

            return teamGroupIds.Count > 0
                ? teamGroupIds
                : activeParticipants.Values.ToHashSet();
        }

        /// <summary>
        /// Kiem tra account co phai Team Leader active cua mot trong cac group hay khong.
        /// </summary>
        private async Task<bool> IsActiveTeamLeaderAsync(Guid accountId, IReadOnlyCollection<Guid> groupIds)
        {
            if (groupIds.Count == 0)
                return false;

            return (await _unitOfWork.Repository<GroupMember>().FindAsync(
                    m => groupIds.Contains(m.GroupId)
                         && m.AccountId == accountId
                         && m.Role == GroupMemberRole.Leader
                         && m.Status == GroupMemberStatus.Active))
                .Any();
        }

        /// <summary>
        /// Lay transaction ky theo approval va transaction id.
        /// </summary>
        private async Task<ApprovalSignatureTransaction?> GetTransactionAsync(Guid approvalId, string transactionId)
            => (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == approvalId && t.TransactionId == transactionId))
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

        /// <summary>
        /// Tao hash dai dien noi dung can ky tu thong tin file, approval va transaction.
        /// </summary>
        private static string ComputeDataToBeSigned(
            FileItem fileItem,
            ApprovalRequest approvalRequest,
            string transactionId)
        {
            var raw = $"{fileItem.Id}{fileItem.Name}{approvalRequest.Id}{transactionId}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
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
                Status = GetString(element, "cert_status_code", "cert_status", "status", "Status")
            };

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
                        return Normalize(statusElement.GetString()) switch
                        {
                            "created" => SignatureTransactionStatus.Created,
                            "waitingconfirm" or "waiting" or "pending" => SignatureTransactionStatus.WaitingConfirm,
                            "signed" or "success" or "completed" => SignatureTransactionStatus.Signed,
                            "failed" or "fail" or "error" or "rejected" => SignatureTransactionStatus.Failed,
                            "expired" => SignatureTransactionStatus.Expired,
                            _ => null
                        };
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

        private sealed record SigningContext(
            ApprovalRequest ApprovalRequest,
            FileItem FileItem,
            Folder Folder);

        private sealed record SigningValidationResult(SigningContext? Context, string? Error)
        {
            public static SigningValidationResult Success(SigningContext context) => new(context, null);
            public static SigningValidationResult Fail(string error) => new(null, error);
        }

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
