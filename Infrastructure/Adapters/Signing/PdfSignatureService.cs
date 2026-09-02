using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.FileVersion;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Services.Signing;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Infrastructure.Adapters;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Layout;
using iText.Layout.Properties;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Infrastructure.Adapters.Signing
{
    /// <summary>
    /// Stamp chu ky truc quan "Đã ký số" vao ban PDF/Word/Excel goc sau khi VNPT SmartCA da ky thanh cong,
    /// tao version moi (FileVersionState, qua FileVersionService) cho ban da ky va giu nguyen ban goc trong lich su.
    /// </summary>
    public class PdfSignatureService : IPdfSignatureService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly ICdeStorageKeyBuilder _storageKey;
        private readonly IFolderPermissionService _permission;
        private readonly IOfficeToPdfConverter _officeConverter;
        private readonly ICadToPdfConverter _cadConverter;
        private readonly IFileVersionService _fileVersionService;
        private readonly ILogger<PdfSignatureService> _logger;

        public PdfSignatureService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            ICdeStorageKeyBuilder storageKey,
            IFolderPermissionService permission,
            IOfficeToPdfConverter officeConverter,
            ICadToPdfConverter cadConverter,
            IFileVersionService fileVersionService,
            ILogger<PdfSignatureService> logger)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _storageKey = storageKey;
            _permission = permission;
            _officeConverter = officeConverter;
            _cadConverter = cadConverter;
            _fileVersionService = fileVersionService;
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

            var currentVersion = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            var isPdf = fileItem.FileType == FileType.Pdf;
            var isWord = FileSignatureFormatRules.IsWordFormat(currentVersion.Format);
            var isExcel = FileSignatureFormatRules.IsExcelFormat(currentVersion.Format);
            var isCad2D = fileItem.FileType == FileType.Cad && FileSignatureFormatRules.IsCad2DFormat(currentVersion.Format);
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
            var pendingSignerName = ResolveSignerDisplayName(
                pendingSignerCertificateDer,
                pendingAccount?.UserName ?? pendingSignerId.ToString());
            stampSigners.Add(new SignerStampInfo(
                pendingSignerName,
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
                var existingVersion = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.SignedVersionId.Value);
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

            //var stampSigners = await BuildStampSignersAsync(approval.Id);
            //if (stampSigners.Count == 0)
            //{
            //    var fallbackAccount = transaction.SignedBy.HasValue
            //        ? await _unitOfWork.Repository<Account>().GetByIdAsync(transaction.SignedBy.Value)
            //        : null;
            //    stampSigners = new[]
            //    {
            //        new SignerStampInfo(
            //            fallbackAccount?.UserName ?? transaction.SignedBy?.ToString() ?? actor.ToString(),
            //            transaction.SignedAt ?? DateTime.UtcNow,
            //            transaction.CertificateSerial)
            //    };
            //}

            //if (!fileItem.CurrentVersionId.HasValue)
            //    return ApiResponse.Fail("File has no content version.");

            //var currentVersion = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value);
            //if (currentVersion == null || currentVersion.StoragePath == null)
            //    return ApiResponse.Fail("Current version not found.");

            //var isPdf = fileItem.FileType == FileType.Pdf;
            //var isWord = IsWordFormat(currentVersion.Format);
            //var isExcel = IsExcelFormat(currentVersion.Format);
            //var isCad2D = fileItem.FileType == FileType.Cad && IsCad2DFormat(currentVersion.Format);
            //if (!isPdf && !isWord && !isExcel && !isCad2D)
            //    return ApiResponse.Fail("Only PDF, Word, Excel and 2D CAD (DWG/DWGX) files support visual signature.");

            //var position = (await _unitOfWork.Repository<FileSignaturePosition>().FindAsync(
            //        p => p.FileItemId == fileItem.Id))
            //    .FirstOrDefault();
            //if (position == null)
            //    return ApiResponse.Fail("Signature position must be set before signing.");

            FileVersionState signedVersion;
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
                var objectName = await _storageKey.ForDerivedAsync(fileItem.FolderId, DerivedFileKind.Signed, signedExtension);
                var stored = await _storage.SaveAsync(output, objectName);

                var now = DateTime.UtcNow;

                // Bản đã ký = version thay thế (WorkingVersion +1) qua FileVersionService,
                // kèm metadata chữ ký — không tự ghi bảng version nữa. Dùng AppendSignedVersionAsync
                // chứ không phải đường upload: nội dung không đổi nên mô tả/cảnh báo AI phải đi theo.
                var versionResult = await _fileVersionService.AppendSignedVersionAsync(
                    fileItem.Id, fileItem.Name,
                    new FileVersionDataDTO
                    {
                        StoragePath = stored.RelativePath,
                        FileSizeBytes = stored.SizeBytes,
                        Format = signedFormat,
                        Checksum = stored.Checksum,
                        UploadedByAccountId = actor,
                        IsSigned = true,
                        SignedAt = signedAt,
                        SignedBy = signedBy,
                        CertificateSerial = transaction.CertificateSerial
                    });

                signedVersion = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(versionResult.VersionStateId!.Value)
                    ?? throw new InvalidOperationException("Signed version state not found after creation.");

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

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.SignedVersionId.Value);
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

        private async Task<IReadOnlyList<SignerStampInfo>> BuildStampSignersAsync(Guid approvalId)
        {
            var signedTransactions = (await _unitOfWork.Repository<ApprovalSignatureTransaction>().FindAsync(
                    t => t.ApprovalRequestId == approvalId
                         && t.Status == SignatureTransactionStatus.Signed
                         && t.SignedBy.HasValue))
                .OrderBy(t => t.SignedAt ?? t.CreatedAt)
                .ToList();

            var accountIds = signedTransactions.Select(t => t.SignedBy.Value).Distinct().ToList();
            var accounts = (await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id)))
                .ToDictionary(a => a.Id);

            return signedTransactions
                .Select(t =>
                {
                    var fallbackName = accounts.TryGetValue(t.SignedBy.Value, out var account)
                        ? account.UserName
                        : t.SignedBy.Value.ToString();
                    var name = ResolveSignerDisplayName(t.SignerCertificateBase64, fallbackName);
                    return new SignerStampInfo(name, t.SignedAt ?? t.CreatedAt, t.CertificateSerial, t.TransactionId);
                })
                .ToList();
        }

        // Ten hien thi tren chu ky uu tien lay theo CCCD (Common Name trong chung thu so VNPT SmartCA -
        // ten that da duoc VNPT xac thuc khi cap chung thu), chi fallback ve UserName he thong neu khong
        // doc duoc chung thu (chua ky / chung thu loi).
        private static string ResolveSignerDisplayName(byte[]? certificateDer, string fallbackName)
            => TryGetCertificateCommonName(certificateDer) ?? fallbackName;

        private static string ResolveSignerDisplayName(string? certificateBase64, string fallbackName)
            => ResolveSignerDisplayName(TryDecodeBase64(certificateBase64), fallbackName);

        private static byte[]? TryDecodeBase64(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            try { return Convert.FromBase64String(base64); }
            catch (FormatException) { return null; }
        }

        private static string? TryGetCertificateCommonName(byte[]? certificateDer)
        {
            if (certificateDer == null || certificateDer.Length == 0) return null;
            try
            {
                using var certificate = X509CertificateLoader.LoadCertificate(certificateDer);
                var commonName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                return string.IsNullOrWhiteSpace(commonName) ? null : commonName.Trim();
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        private async Task<SignedFileInfoResponseDTO> BuildSignedFileInfoAsync(
            FileItem fileItem,
            FileVersionState signedVersion,
            ApprovalSignatureTransaction? transaction)
        {
            var signerAccount = signedVersion.SignedBy.HasValue
                ? await _unitOfWork.Repository<Account>().GetByIdAsync(signedVersion.SignedBy.Value)
                : null;
            var url = await _storage.GetPresignedUrlAsync(signedVersion.StoragePath!, 60);
            var signedByName = signerAccount != null
                ? ResolveSignerDisplayName(transaction?.SignerCertificateBase64, signerAccount.UserName)
                : null;

            return new SignedFileInfoResponseDTO
            {
                Id = signedVersion.Id,
                FileItemId = fileItem.Id,
                FileName = $"{fileItem.Name}_signed.{FileSignatureFormatRules.NormalizeFormat(signedVersion.Format)}",
                SignedVersionId = signedVersion.Id,
                VersionNumber = signedVersion.WorkingVersion,
                StoragePath = signedVersion.StoragePath,
                Url = url,
                SignedAt = signedVersion.SignedAt,
                SignedBy = signedByName,
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
            FileVersionState currentVersion,
            FileSignaturePosition position,
            IReadOnlyList<SignerStampInfo> signers)
        {
            MemoryStream pdfStream;
            if (!string.IsNullOrWhiteSpace(currentVersion.PreviewStoragePath))
            {
                pdfStream = await OpenSeekableReadStreamAsync(currentVersion.PreviewStoragePath);
            }
            else if (FileSignatureFormatRules.IsCad2DFormat(currentVersion.Format))
            {
                var ext = "." + FileSignatureFormatRules.NormalizeFormat(currentVersion.Format);
                await using var source = await _storage.OpenReadAsync(currentVersion.StoragePath!);
                await using var converted = await _cadConverter.ConvertToPdfAsync(source, ext);
                pdfStream = new MemoryStream();
                await converted.CopyToAsync(pdfStream);
                pdfStream.Position = 0;
            }
            else
            {
                var ext = "." + FileSignatureFormatRules.NormalizeFormat(currentVersion.Format);
                await using var source = await _storage.OpenReadAsync(currentVersion.StoragePath!);
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
                DrawSignatureStamp(pdfCanvas, position, pdfY, signers, pageHeight);
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

        // Cac timestamp trong he thong luu UTC (DateTime.UtcNow); Viet Nam khong co DST nen +7h co dinh la du,
        // khong can TimeZoneInfo (tranh phu thuoc ten timezone khac nhau giua Windows/Linux).
        private static DateTime ToVietnamTime(DateTime utc) => utc.AddHours(7);

        private static bool IsExplicitSignerApproval(ApprovalRequest approval)
            => approval.FromZone == CdeArea.Shared && approval.TargetZone == CdeArea.Published;

        private static readonly Lazy<byte[]> _regularFontBytes = new(() => EmbeddedResourceLoader.LoadFontBytes("NotoSans-Regular.ttf"));
        private static readonly Lazy<byte[]> _boldFontBytes = new(() => EmbeddedResourceLoader.LoadFontBytes("NotoSans-Bold.ttf"));

        // Font Helvetica mac dinh cua iText khong co dau tieng Viet -> phai nhung font Unicode (NotoSans) rieng.
        private static PdfFont GetRegularFont() => PdfFontFactory.CreateFont(_regularFontBytes.Value, PdfEncodings.IDENTITY_H);
        private static PdfFont GetBoldFont() => PdfFontFactory.CreateFont(_boldFontBytes.Value, PdfEncodings.IDENTITY_H);

        internal static void DrawSignatureStamp(
            iText.Kernel.Pdf.Canvas.PdfCanvas pdfCanvas,
            FileSignaturePosition position,
            float pdfY,
            IReadOnlyList<SignerStampInfo> signers,
            float pageHeight)
        {
            var validColor = new DeviceRgb(21, 128, 61);      // green-700: vien + tieu de "hop le"
            var labelColor = new DeviceRgb(185, 28, 28);      // red-700: nhan "Ky boi" - do, dam hon ban goc cho do garish
            var nameColor = new DeviceRgb(153, 27, 27);       // red-800: ten nguoi ky - do dam nhat, bold noi bat
            var timestampColor = new DeviceRgb(185, 28, 28);  // red-700: ngay gio
            const float cornerRadius = 3f;

            const float padding = 6f;
            const float lineLeading = 1.35f;
            const float bottomMargin = 12f;
            const float minDetailFontSize = 4.5f;
            const float fontStep = 0.25f;

            var titleFontSize = Math.Clamp(position.Height * 0.17f, 7f, 14f);
            var headerHeight = Math.Min(16f, position.Height * 0.32f);
            var topY = pdfY + position.Height; // canh tren co dinh
            var maxAvailableHeight = Math.Max(headerHeight + padding * 1.5f + 2f, topY - bottomMargin);
            var bodyWidth = Math.Max(1f, position.Width - padding * 2);

            // Uoc luong "2 dong/nguoi ky" truoc day sai khi ten dai (vd "Ký bởi: TRƯƠNG THỊ THANH HUYỀN")
            // tu xuong dong trong khung hep - lam thieu chieu cao cap phat va iText Canvas AM THAM CAT BOT
            // nhung nguoi ky cuoi danh sach (khong loi, chi khong ve). Do do phai ĐO chieu cao THUC TE bang
            // chinh engine layout cua iText (khong con uoc luong so dong) roi moi giam font neu con thieu cho.
            // Viec do dac dung mot PdfFont/PdfDocument tam rieng (xem MeasureParagraphHeight) - KHONG dung
            // chung PdfFont voi ban ve that, vi mot PdfFont da "dinh" vao PdfDocument nao thi khong the ghi
            // sang PdfDocument khac (loi "indirect object belongs to other PDF document").
            var detailFontSize = Math.Clamp(position.Height * 0.11f, minDetailFontSize, 8.5f);
            float requiredBodyHeight;
            while (true)
            {
                var measuredHeight = MeasureSignerDetailsHeight(signers, detailFontSize, lineLeading, bodyWidth);
                requiredBodyHeight = measuredHeight + padding;
                var availableBodyHeight = maxAvailableHeight - headerHeight - padding * 1.5f;
                if (requiredBodyHeight <= availableBodyHeight || detailFontSize <= minDetailFontSize)
                    break;
                detailFontSize = Math.Max(minDetailFontSize, detailFontSize - fontStep);
            }

            var effectiveHeight = Math.Min(maxAvailableHeight, Math.Max(position.Height, headerHeight + requiredBodyHeight + padding * 1.5f));
            pdfY = topY - effectiveHeight; // canh duoi day xuong neu can them cho

            var boldFont = GetBoldFont();
            var regularFont = GetRegularFont();
            var details = BuildSignerDetailsParagraph(signers, boldFont, regularFont, labelColor, nameColor, timestampColor, detailFontSize, lineLeading);

            var headerRect = new Rectangle(
                position.X + padding,
                pdfY + effectiveHeight - headerHeight,
                Math.Max(1, position.Width - padding * 2),
                headerHeight);
            var bodyRect = new Rectangle(
                position.X + padding,
                pdfY + padding,
                Math.Max(1, position.Width - padding * 2),
                Math.Max(1, effectiveHeight - headerHeight - padding * 1.5f));

            pdfCanvas.SaveState()
                .SetFillColor(ColorConstants.WHITE)
                .SetStrokeColor(validColor)
                .SetLineWidth(1.1f)
                .RoundRectangle(position.X, pdfY, position.Width, effectiveHeight, cornerRadius)
                .FillStroke()
                .RestoreState();

            var title = new Paragraph()
                .Add(new Text("Signature Valid").SetFont(boldFont).SetFontSize(titleFontSize).SetFontColor(validColor))
                .SetMargin(0)
                .SetMultipliedLeading(1f)
                .SetTextAlignment(TextAlignment.CENTER);

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

        private static Paragraph BuildSignerDetailsParagraph(
            IReadOnlyList<SignerStampInfo> signers,
            PdfFont boldFont,
            PdfFont regularFont,
            DeviceRgb labelColor,
            DeviceRgb nameColor,
            DeviceRgb timestampColor,
            float detailFontSize,
            float lineLeading)
        {
            var details = new Paragraph()
                .SetMargin(0)
                .SetMultipliedLeading(lineLeading)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetFont(regularFont)
                .SetFontSize(detailFontSize)
                .SetFontColor(labelColor);

            for (var i = 0; i < signers.Count; i++)
            {
                var signer = signers[i];
                var prefix = i == 0 ? "" : "\n";
                details.Add(new Text($"{prefix}Ký bởi: ").SetFontColor(labelColor));
                details.Add(new Text(signer.Name).SetFont(boldFont).SetFontColor(nameColor));
                details.Add(new Text($"\nKý ngày: {ToVietnamTime(signer.SignedAt):dd/MM/yyyy HH:mm:ss}").SetFontColor(timestampColor));
            }

            return details;
        }

        // Do chieu cao THUC TE (co tinh xuong dong theo be rong) ma noi dung cac nguoi ky can khi ve - thay
        // vi uoc luong so dong theo cong thuc, vi ten nguoi ky dai se tu xuong dong va lam sai lech uoc
        // luong, khien iText Canvas (khung co dinh) am tham cat mat cac nguoi ky ve sau trong danh sach.
        // Dung PdfFont/PdfDocument TAM RIENG cho viec do - khong dung chung voi PdfFont dung de ve that,
        // vi mot PdfFont da "dinh" vao PdfDocument nao thi khong flush duoc sang PdfDocument khac.
        private static float MeasureSignerDetailsHeight(
            IReadOnlyList<SignerStampInfo> signers, float detailFontSize, float lineLeading, float width)
        {
            using var measureWriter = new PdfWriter(new MemoryStream());
            using var measureDocument = new iText.Layout.Document(new PdfDocument(measureWriter));
            var measureBoldFont = PdfFontFactory.CreateFont(_boldFontBytes.Value, PdfEncodings.IDENTITY_H);
            var measureRegularFont = PdfFontFactory.CreateFont(_regularFontBytes.Value, PdfEncodings.IDENTITY_H);

            var paragraph = new Paragraph()
                .SetMargin(0)
                .SetMultipliedLeading(lineLeading)
                .SetFont(measureRegularFont)
                .SetFontSize(detailFontSize)
                .SetWidth(width);

            for (var i = 0; i < signers.Count; i++)
            {
                var signer = signers[i];
                var prefix = i == 0 ? "" : "\n";
                paragraph.Add(new Text($"{prefix}Ký bởi: "));
                paragraph.Add(new Text(signer.Name).SetFont(measureBoldFont));
                paragraph.Add(new Text($"\nKý ngày: {ToVietnamTime(signer.SignedAt):dd/MM/yyyy HH:mm:ss}"));
            }

            var renderer = paragraph.CreateRendererSubTree().SetParent(measureDocument.GetRenderer());
            var area = new LayoutArea(1, new Rectangle(0, 0, width, 100_000f));
            var result = renderer.Layout(new LayoutContext(area));
            return result.GetOccupiedArea().GetBBox().GetHeight();
        }
    }
}
