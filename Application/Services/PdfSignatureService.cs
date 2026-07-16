using Application.DTOs.ApiResponseDTO;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Services.Signing;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    /// <summary>
    /// Stamp chu ky truc quan "Đã ký số" vao ban PDF/Word/Excel goc sau khi VNPT SmartCA da ky thanh cong,
    /// tao FileVersion moi cho ban da ky va giu nguyen ban goc.
    /// </summary>
    public class PdfSignatureService : IPdfSignatureService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IFolderPermissionService _permission;
        private readonly IOfficeToPdfConverter _officeConverter;
        private readonly ICadToPdfConverter _cadConverter;
        private readonly ILogger<PdfSignatureService> _logger;

        public PdfSignatureService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            IFolderPermissionService permission,
            IOfficeToPdfConverter officeConverter,
            ICadToPdfConverter cadConverter,
            ILogger<PdfSignatureService> logger)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _permission = permission;
            _officeConverter = officeConverter;
            _cadConverter = cadConverter;
            _logger = logger;
        }

        /// <summary>
        /// Phase 1 cua ky 2 pha: ve khung "CHU KY SO" (goi ca nguoi dang cho ky nay) + dat cho signature
        /// field, tra ve document digest + authenticated attributes can bam va gui cho VNPT ky.
        /// </summary>
        public async Task<PdfExternalSignatureHelper.PreparedSignature> PrepareSignatureAsync(
            Guid approvalId,
            Guid pendingSignerId,
            string pendingCertificateSerial,
            byte[] pendingSignerCertificateDer,
            string pendingTransactionId)
        {
            var approval = await _unitOfWork.Repository<ApprovalRequest>().GetByIdAsync(approvalId)
                ?? throw new ApiExceptionResponse("Approval request not found.", 404);
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(approval.FileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 400);

            var currentVersion = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            var isPdf = fileItem.FileType == FileType.Pdf;
            var isWord = IsWordFormat(currentVersion.Format);
            var isExcel = IsExcelFormat(currentVersion.Format);
            var isCad2D = fileItem.FileType == FileType.Cad && IsCad2DFormat(currentVersion.Format);
            if (!isPdf && !isWord && !isExcel && !isCad2D)
                throw new ApiExceptionResponse("Only PDF, Word, Excel and 2D CAD (DWG/DWGX) files support visual signature.", 400);

            var position = (await _unitOfWork.Repository<FileSignaturePosition>().FindAsync(
                    p => p.FileItemId == fileItem.Id))
                .FirstOrDefault()
                ?? throw new ApiExceptionResponse("Signature position must be set before signing.", 400);

            // Danh sach nguoi da ky (Status=Signed) + nguoi dang cho ky nay (chua co transaction Signed) -
            // hien thi truoc, nhung chi 1 nguoi (nguoi hoan tat cuoi cung) moi thuc su tao chu ky mat ma.
            var stampSigners = (await BuildStampSignersAsync(approval.Id)).ToList();
            var pendingAccount = await _unitOfWork.Repository<Account>().GetByIdAsync(pendingSignerId);
            stampSigners.Add(new SignerStampInfo(
                pendingAccount?.UserName ?? pendingSignerId.ToString(),
                DateTime.UtcNow,
                pendingCertificateSerial,
                pendingTransactionId));

            var stampedBytes = isPdf
                ? await StampPdfSignatureAsync(currentVersion.StoragePath, position, stampSigners)
                : await StampOfficeAsConvertedPdfAsync(currentVersion, position, stampSigners);

            return PdfExternalSignatureHelper.PrepareForSigning(stampedBytes, pendingSignerCertificateDer);
        }

        public async Task<ApiResponse> GenerateSignedPdfAsync(Guid approvalId, Guid actor)
        {
            var approval = await _unitOfWork.Repository<ApprovalRequest>().GetByIdAsync(approvalId);
            if (approval == null)
                return ApiResponse.Fail("Approval request not found.");

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(approval.FileItemId);
            if (fileItem == null)
                return ApiResponse.Fail("File not found.");

            // Idempotent: neu file da ky xong (vd goi lai sau khi zone da chuyen sang Shared), tra luon ket qua cu.
            if (fileItem.IsSigned && fileItem.SignedVersionId.HasValue)
            {
                var existingVersion = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.SignedVersionId.Value);
                if (existingVersion != null)
                {
                    var existingTransaction = await GetLatestSignedTransactionAsync(fileItem.Id, approvalId);
                    var existingInfo = await BuildSignedFileInfoAsync(fileItem, existingVersion, existingTransaction);
                    return ApiResponse.Success("Signed PDF already generated", existingInfo);
                }
            }

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);
            if (folder == null)
                return ApiResponse.Fail("File folder not found.");

            if (!approval.RequiresSignature)
                return ApiResponse.Fail("This file does not require digital signature.");

            if (approval.Status != ApprovalRequestStatus.Pending)
                return ApiResponse.Fail("Approval request must be pending.");

            var transaction = await GetLatestSignedTransactionAsync(fileItem.Id, approvalId);
            if (transaction == null)
                return ApiResponse.Fail("SmartCA signing transaction must be completed (Signed) before generating signed PDF.");

            if (string.IsNullOrWhiteSpace(transaction.PreparedPdfStoragePath)
                || string.IsNullOrWhiteSpace(transaction.DigestBase64)
                || string.IsNullOrWhiteSpace(transaction.SignedAttributesBase64)
                || string.IsNullOrWhiteSpace(transaction.SignerCertificateBase64)
                || string.IsNullOrWhiteSpace(transaction.SignatureValueBase64))
            {
                return ApiResponse.Fail(
                    "Signing transaction is missing prepared signature data (2-phase signing was not completed). Please re-sign.");
            }

            var signers = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                    s => s.ApprovalRequestId == approval.Id))
                .ToList();
            if (IsExplicitSignerApproval(approval)
                && (signers.Count == 0 || signers.Any(s => s.Status != ApprovalRequestSignerStatus.Signed)))
                return ApiResponse.Fail("All required digital signers must sign before generating signed PDF.");

            FileVersion signedVersion;
            try
            {
                var signedBy = transaction.SignedBy ?? actor;
                var signedAt = transaction.SignedAt ?? DateTime.UtcNow;
                var signedFormat = "pdf";
                var signedExtension = $".{signedFormat}";

                using var preparedBuffer = await OpenSeekableReadStreamAsync(transaction.PreparedPdfStoragePath);
                var preparedPdfBytes = preparedBuffer.ToArray();

                var stampedBytes = PdfExternalSignatureHelper.CompleteSigning(
                    preparedPdfBytes,
                    Convert.FromBase64String(transaction.DigestBase64),
                    Convert.FromBase64String(transaction.SignedAttributesBase64),
                    Convert.FromBase64String(transaction.SignatureValueBase64),
                    Convert.FromBase64String(transaction.SignerCertificateBase64));

                using var output = new MemoryStream(stampedBytes);
                var stored = await _storage.SaveAsync(output, folder.ProjectId, fileItem.FolderId, signedExtension);

                var now = DateTime.UtcNow;
                var nextVersionNumber = (await _unitOfWork.Repository<FileVersion>().FindAsync(
                        v => v.FileItemId == fileItem.Id))
                    .Select(v => v.VersionNumber)
                    .DefaultIfEmpty(0)
                    .Max() + 1;

                signedVersion = new FileVersion
                {
                    Id = Guid.NewGuid(),
                    FileItemId = fileItem.Id,
                    VersionNumber = nextVersionNumber,
                    StoragePath = stored.RelativePath,
                    FileSizeBytes = stored.SizeBytes,
                    Format = signedFormat,
                    Checksum = stored.Checksum,
                    IsHidden = false,
                    UploadedByAccountId = actor,
                    UploadedAt = now,
                    IsSigned = true,
                    SignedAt = signedAt,
                    SignedBy = signedBy,
                    CertificateSerial = transaction.CertificateSerial
                };

                await _unitOfWork.Repository<FileVersion>().CreateAsync(signedVersion);

                fileItem.SignedVersionId = signedVersion.Id;
                fileItem.CurrentVersionId = signedVersion.Id;
                fileItem.IsSigned = true;
                fileItem.UpdatedAt = now;
                if (fileItem.FileType != FileType.Pdf)
                    fileItem.FileType = FileType.Pdf;

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate signed PDF for approval {ApprovalId}", approvalId);
                var reason = ex.InnerException?.Message ?? ex.Message;
                return ApiResponse.Fail(
                    $"SmartCA signed successfully but signed file generation failed: {reason}",
                    new
                    {
                        errorType = ex.GetType().Name,
                        message = ex.Message,
                        innerMessage = ex.InnerException?.Message
                    });
            }

            var info = await BuildSignedFileInfoAsync(fileItem, signedVersion, transaction);
            return ApiResponse.Success("Signed file generated successfully", info);
        }

        public async Task<ApiResponse> GetSignedFileInfoAsync(Guid fileItemId, Guid actor)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (fileItem == null)
                return ApiResponse.Fail("File not found.");

            //await _permission.RequireAsync(actor, fileItem.FolderId, FolderAction.Download);

            if (!fileItem.SignedVersionId.HasValue)
                return ApiResponse.Fail("Signed file not available.");

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.SignedVersionId.Value);
            if (version == null)
                return ApiResponse.Fail("Signed version not found.");

            var transaction = await GetLatestSignedTransactionAsync(fileItem.Id);

            var info = await BuildSignedFileInfoAsync(fileItem, version, transaction);
            return ApiResponse.Success("Signed file info retrieved", info);
        }

        /// <summary>Lay transaction Signed gan nhat cua file (loc theo approvalId neu co).</summary>
        private async Task<ApprovalSignatureTransaction?> GetLatestSignedTransactionAsync(Guid fileItemId, Guid? approvalId = null)
            => (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => (!approvalId.HasValue || t.ApprovalRequestId == approvalId)
                         && t.FileItemId == fileItemId
                         && t.Status == SignatureTransactionStatus.Signed))
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();

        internal readonly record struct SignerStampInfo(string Name, DateTime SignedAt, string? CertificateSerial, string? TransactionId);

        /// <summary>
        /// Lay danh sach TAT CA nguoi thuc su da ky (moi tai khoan 1 dong, theo transaction Signed gan nhat
        /// cua chinh ho) de khung dau chu ky the hien day du, khong chi 1 nguoi ky sau cung.
        /// </summary>
        private async Task<IReadOnlyList<SignerStampInfo>> BuildStampSignersAsync(Guid approvalId)
        {
            var latestPerAccount = (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == approvalId
                         && t.Status == SignatureTransactionStatus.Signed
                         && t.SignedBy.HasValue))
                .GroupBy(t => t.SignedBy!.Value)
                .Select(g => g.OrderByDescending(t => t.CreatedAt).First())
                .OrderBy(t => t.SignedAt ?? t.CreatedAt)
                .ToList();

            var accountIds = latestPerAccount.Select(t => t.SignedBy!.Value).ToList();
            var accounts = (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                .ToDictionary(a => a.Id);

            return latestPerAccount
                .Select(t => new SignerStampInfo(
                    accounts.TryGetValue(t.SignedBy!.Value, out var account) ? account.UserName : t.SignedBy.Value.ToString(),
                    t.SignedAt ?? t.CreatedAt,
                    t.CertificateSerial,
                    t.TransactionId))
                .ToList();
        }

        private async Task<SignedFileInfoResponseDTO> BuildSignedFileInfoAsync(
            FileItem fileItem,
            FileVersion signedVersion,
            ApprovalSignatureTransaction? transaction)
        {
            var signerAccount = signedVersion.SignedBy.HasValue
                ? await _unitOfWork.Repository<Account>().GetByIdAsync(signedVersion.SignedBy.Value)
                : null;
            var url = await _storage.GetPresignedUrlAsync(signedVersion.StoragePath, 60);

            return new SignedFileInfoResponseDTO
            {
                Id = signedVersion.Id,
                FileItemId = fileItem.Id,
                FileName = $"{fileItem.Name}_signed.{NormalizeSignedFormat(signedVersion.Format)}",
                SignedVersionId = signedVersion.Id,
                VersionNumber = signedVersion.VersionNumber,
                StoragePath = signedVersion.StoragePath,
                Url = url,
                SignedAt = signedVersion.SignedAt,
                SignedBy = signerAccount?.UserName,
                CertificateSerial = signedVersion.CertificateSerial,
                TransactionId = transaction?.TransactionId
            };
        }

        private async Task<byte[]> StampPdfSignatureAsync(
            string storagePath,
            FileSignaturePosition position,
            IReadOnlyList<SignerStampInfo> signers)
        {
            using var inputStream = await OpenSeekableReadStreamAsync(storagePath);
            return StampPdfBytes(inputStream, position, signers);
        }

        private async Task<byte[]> StampOfficeAsConvertedPdfAsync(
            FileVersion currentVersion,
            FileSignaturePosition position,
            IReadOnlyList<SignerStampInfo> signers)
        {
            MemoryStream pdfStream;
            if (!string.IsNullOrWhiteSpace(currentVersion.PreviewStoragePath))
            {
                pdfStream = await OpenSeekableReadStreamAsync(currentVersion.PreviewStoragePath);
            }
            else if (IsCad2DFormat(currentVersion.Format))
            {
                var ext = "." + currentVersion.Format.Trim().TrimStart('.').ToLowerInvariant();
                await using var source = await _storage.OpenReadAsync(currentVersion.StoragePath);
                await using var converted = await _cadConverter.ConvertToPdfAsync(source, ext);
                pdfStream = new MemoryStream();
                await converted.CopyToAsync(pdfStream);
                pdfStream.Position = 0;
            }
            else
            {
                var ext = "." + currentVersion.Format.Trim().TrimStart('.').ToLowerInvariant();
                await using var source = await _storage.OpenReadAsync(currentVersion.StoragePath);
                await using var converted = await _officeConverter.ConvertToPdfAsync(source, ext);
                pdfStream = new MemoryStream();
                await converted.CopyToAsync(pdfStream);
                pdfStream.Position = 0;
            }

            using (pdfStream)
            {
                return StampPdfBytes(pdfStream, position, signers);
            }
        }

        private static byte[] StampPdfBytes(
            MemoryStream inputStream,
            FileSignaturePosition position,
            IReadOnlyList<SignerStampInfo> signers)
        {
            using var outputStream = new MemoryStream();
            var reader = new PdfReader(inputStream);
            var writer = new PdfWriter(outputStream);
            writer.SetCloseStream(false); // khong de PdfDocument.Close() dong luon outputStream (MemoryStream con can doc sau)

            using (var pdfDocument = new PdfDocument(reader, writer))
            {
                var page = pdfDocument.GetPage(position.PageNumber);
                var pageHeight = page.GetPageSize().GetHeight();
                var pdfY = pageHeight - position.Y - position.Height;

                var pdfCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
                DrawSignatureStamp(pdfCanvas, position, pdfY, signers);
            }

            return outputStream.ToArray();
        }

        private async Task<MemoryStream> OpenSeekableReadStreamAsync(string storagePath)
        {
            await using var source = await _storage.OpenReadAsync(storagePath);
            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            buffer.Position = 0;
            return buffer;
        }

        private static bool IsWordFormat(string? format)
            => NormalizeSignedFormat(format) is "doc" or "docx";

        private static bool IsExcelFormat(string? format)
            => NormalizeSignedFormat(format) is "xls" or "xlsx";

        // Chi dwg/dwgx - ConvertAPI (dich vu dang dung de convert CAD 2D -> PDF) chi ho tro 2 dinh dang nay.
        private static bool IsCad2DFormat(string? format)
            => NormalizeSignedFormat(format) is "dwg" or "dwgx";

        // Cac timestamp trong he thong luu UTC (DateTime.UtcNow); Viet Nam khong co DST nen +7h co dinh la du,
        // khong can TimeZoneInfo (tranh phu thuoc ten timezone khac nhau giua Windows/Linux).
        private static DateTime ToVietnamTime(DateTime utc) => utc.AddHours(7);

        private static bool IsExplicitSignerApproval(ApprovalRequest approval)
            => approval.FromZone == CdeArea.Shared && approval.TargetZone == CdeArea.Published;

        private static string NormalizeSignedFormat(string? format)
        {
            var normalized = (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            return string.IsNullOrWhiteSpace(normalized) ? "pdf" : normalized;
        }

        private static readonly Lazy<byte[]> _regularFontBytes = new(() => LoadEmbeddedFontBytes("NotoSans-Regular.ttf"));
        private static readonly Lazy<byte[]> _boldFontBytes = new(() => LoadEmbeddedFontBytes("NotoSans-Bold.ttf"));

        // Font Helvetica mac dinh cua iText khong co dau tieng Viet -> phai nhung font Unicode (NotoSans) rieng.
        private static PdfFont GetRegularFont() => PdfFontFactory.CreateFont(_regularFontBytes.Value, PdfEncodings.IDENTITY_H);
        private static PdfFont GetBoldFont() => PdfFontFactory.CreateFont(_boldFontBytes.Value, PdfEncodings.IDENTITY_H);

        private static byte[] LoadEmbeddedFontBytes(string fileName)
        {
            var assembly = typeof(PdfSignatureService).Assembly;
            var resourceName = $"Application.Resources.Fonts.{fileName}";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded font resource not found: {resourceName}");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        internal static void DrawSignatureStamp(
            iText.Kernel.Pdf.Canvas.PdfCanvas pdfCanvas,
            FileSignaturePosition position,
            float pdfY,
            IReadOnlyList<SignerStampInfo> signers)
        {
            var validColor = new DeviceRgb(22, 163, 74);    // green-600: vien + tieu de "hop le"
            var detailColor = new DeviceRgb(220, 38, 38);   // red-600: dong "Ky boi"/"Ky ngay"

            const float padding = 5f;
            const float lineLeading = 1.25f;
            const float detailFontSize = 5.5f; // co chu co dinh, de doc du ky bao nhieu nguoi

            // Khung co the cao hon khung nguoi dung ve (mo rong xuong duoi trang) neu nhieu nguoi ky can
            // nhieu dong hon cho voi cha - giu cua tren co dinh dung vi tri nguoi dung dat, chi day canh
            // duoi xuong. Tranh tinh huong co chu bi ep nho toi muc kho doc khi co nhieu nguoi ky.
            var titleFontSize = Math.Clamp(position.Height * 0.16f, 6.5f, 13f);
            var headerHeight = Math.Min(14f, position.Height * 0.32f);
            var requiredBodyHeight = signers.Count * detailFontSize * lineLeading + padding;
            var effectiveHeight = Math.Max(position.Height, headerHeight + requiredBodyHeight + padding * 1.5f);
            var topY = pdfY + position.Height; // canh tren co dinh
            pdfY = topY - effectiveHeight; // canh duoi day xuong neu can them cho

            var headerRect = new Rectangle(position.X, pdfY + effectiveHeight - headerHeight, position.Width, headerHeight);
            var bodyRect = new Rectangle(
                position.X + padding,
                pdfY + padding,
                Math.Max(1, position.Width - padding * 2),
                Math.Max(1, effectiveHeight - headerHeight - padding * 1.5f));

            pdfCanvas.SaveState()
                .SetFillColor(ColorConstants.WHITE)
                .SetStrokeColor(validColor)
                .SetLineWidth(1f)
                .Rectangle(position.X, pdfY, position.Width, effectiveHeight)
                .FillStroke()
                .RestoreState();

            var boldFont = GetBoldFont();
            var regularFont = GetRegularFont();

            var title = new Paragraph()
                .Add(new Text("Signature Valid").SetFont(boldFont).SetFontSize(titleFontSize).SetFontColor(validColor))
                .SetMargin(0)
                .SetMultipliedLeading(1f)
                .SetTextAlignment(TextAlignment.LEFT);

            var details = new Paragraph()
                .SetMargin(0)
                .SetMultipliedLeading(lineLeading)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetFont(regularFont)
                .SetFontSize(detailFontSize)
                .SetFontColor(detailColor);

            for (var i = 0; i < signers.Count; i++)
            {
                var signer = signers[i];
                var prefix = i == 0 ? "" : "\n";
                details.Add(new Text($"{prefix}Ký bởi: ").SetFontColor(detailColor));
                details.Add(new Text(signer.Name).SetFont(boldFont).SetFontColor(detailColor));
                details.Add(new Text($" — {ToVietnamTime(signer.SignedAt):dd/MM/yyyy HH:mm:ss}").SetFontColor(detailColor));
            }

            using (var headerCanvas = new Canvas(pdfCanvas, headerRect))
            {
                headerCanvas.Add(title);
            }

            // Bao trong Div cao bang ca bodyRect + can giua theo chieu doc, tranh chu dinh o tren de lai
            // khoang trong o duoi khi khung nguoi dung ve cao hon noi dung thuc te.
            var bodyWrapper = new Div()
                .SetHeight(bodyRect.GetHeight())
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .Add(details);

            using (var bodyCanvas = new Canvas(pdfCanvas, bodyRect))
            {
                bodyCanvas.Add(bodyWrapper);
            }
        }
    }
}
