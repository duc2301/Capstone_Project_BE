using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.FileVersion;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
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
        private readonly IFileVersionService _fileVersionService;
        private readonly ILogger<PdfSignatureService> _logger;

        public PdfSignatureService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            IFolderPermissionService permission,
            IOfficeToPdfConverter officeConverter,
            ICadToPdfConverter cadConverter,
            IFileVersionService fileVersionService,
            ILogger<PdfSignatureService> logger)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _permission = permission;
            _officeConverter = officeConverter;
            _cadConverter = cadConverter;
            _fileVersionService = fileVersionService;
            _logger = logger;
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

            var signers = (await _unitOfWork.Repository<ApprovalRequestSigner>().FindAsync(
                    s => s.ApprovalRequestId == approval.Id))
                .ToList();
            if (IsExplicitSignerApproval(approval)
                && (signers.Count == 0 || signers.Any(s => s.Status != ApprovalRequestSignerStatus.Signed)))
                return ApiResponse.Fail("All required digital signers must sign before generating signed PDF.");

            var stampSigners = await BuildStampSignersAsync(approval.Id);
            if (stampSigners.Count == 0)
            {
                var fallbackAccount = transaction.SignedBy.HasValue
                    ? await _unitOfWork.Repository<Account>().GetByIdAsync(transaction.SignedBy.Value)
                    : null;
                stampSigners = new[]
                {
                    new SignerStampInfo(
                        fallbackAccount?.UserName ?? transaction.SignedBy?.ToString() ?? actor.ToString(),
                        transaction.SignedAt ?? DateTime.UtcNow,
                        transaction.CertificateSerial)
                };
            }

            if (!fileItem.CurrentVersionId.HasValue)
                return ApiResponse.Fail("File has no content version.");

            var currentVersion = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value);
            if (currentVersion == null || currentVersion.StoragePath == null)
                return ApiResponse.Fail("Current version not found.");

            var isPdf = fileItem.FileType == FileType.Pdf;
            var isWord = IsWordFormat(currentVersion.Format);
            var isExcel = IsExcelFormat(currentVersion.Format);
            var isCad2D = fileItem.FileType == FileType.Cad && IsCad2DFormat(currentVersion.Format);
            if (!isPdf && !isWord && !isExcel && !isCad2D)
                return ApiResponse.Fail("Only PDF, Word, Excel and 2D CAD (DWG/DWGX) files support visual signature.");

            var position = (await _unitOfWork.Repository<FileSignaturePosition>().FindAsync(
                    p => p.FileItemId == fileItem.Id))
                .FirstOrDefault();
            if (position == null)
                return ApiResponse.Fail("Signature position must be set before signing.");

            FileVersionState signedVersion;
            try
            {
                var signedBy = transaction.SignedBy ?? actor;
                var signedAt = transaction.SignedAt ?? DateTime.UtcNow;
                var signedFormat = "pdf";
                var signedExtension = $".{signedFormat}";

                var stampedBytes = isPdf
                    ? await StampPdfSignatureAsync(currentVersion.StoragePath, position!, stampSigners)
                    : await StampOfficeAsConvertedPdfAsync(currentVersion, position!, stampSigners);

                using var output = new MemoryStream(stampedBytes);
                var stored = await _storage.SaveAsync(output, folder.ProjectId, fileItem.FolderId, signedExtension);

                var now = DateTime.UtcNow;

                // Bản đã ký = version thay thế (WorkingVersion +1) qua FileVersionService,
                // kèm metadata chữ ký — không tự ghi bảng version nữa.
                var versionResult = await _fileVersionService.GetNextUploadVersionAsync(
                    fileItem.FolderId, fileItem.Name,
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
                if (!isPdf)
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

        private readonly record struct SignerStampInfo(string Name, DateTime SignedAt, string? CertificateSerial);

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
                    t.CertificateSerial))
                .ToList();
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

            return new SignedFileInfoResponseDTO
            {
                Id = signedVersion.Id,
                FileItemId = fileItem.Id,
                FileName = $"{fileItem.Name}_signed.{NormalizeSignedFormat(signedVersion.Format)}",
                SignedVersionId = signedVersion.Id,
                VersionNumber = signedVersion.WorkingVersion,
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
            FileVersionState currentVersion,
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
                var ext = "." + NormalizeSignedFormat(currentVersion.Format);
                await using var source = await _storage.OpenReadAsync(currentVersion.StoragePath!);
                await using var converted = await _cadConverter.ConvertToPdfAsync(source, ext);
                pdfStream = new MemoryStream();
                await converted.CopyToAsync(pdfStream);
                pdfStream.Position = 0;
            }
            else
            {
                var ext = "." + NormalizeSignedFormat(currentVersion.Format);
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

        private static void DrawSignatureStamp(
            iText.Kernel.Pdf.Canvas.PdfCanvas pdfCanvas,
            FileSignaturePosition position,
            float pdfY,
            IReadOnlyList<SignerStampInfo> signers)
        {
            var lineColor = new DeviceRgb(30, 41, 59);      // slate-800: vien + tieu de
            var nameColor = new DeviceRgb(15, 23, 42);      // slate-900: ten nguoi ky
            var mutedColor = new DeviceRgb(100, 116, 139);  // slate-500: chi tiet phu

            const float padding = 5f;
            var headerHeight = Math.Min(14f, position.Height * 0.28f);
            var headerRect = new Rectangle(position.X, pdfY + position.Height - headerHeight, position.Width, headerHeight);
            var bodyRect = new Rectangle(
                position.X + padding,
                pdfY + padding,
                Math.Max(1, position.Width - padding * 2),
                Math.Max(1, position.Height - headerHeight - padding * 1.5f));

            pdfCanvas.SaveState()
                .SetFillColor(ColorConstants.WHITE)
                .SetStrokeColor(lineColor)
                .SetLineWidth(0.75f)
                .Rectangle(position.X, pdfY, position.Width, position.Height)
                .FillStroke()
                .SetLineWidth(0.5f)
                .MoveTo(position.X, headerRect.GetY())
                .LineTo(position.X + position.Width, headerRect.GetY())
                .Stroke()
                .RestoreState();

            var boldFont = GetBoldFont();
            var regularFont = GetRegularFont();

            var title = new Paragraph()
                .Add(new Text("CHỮ KÝ SỐ").SetFont(boldFont).SetFontSize(7.5f).SetFontColor(lineColor).SetCharacterSpacing(0.4f))
                .SetMargin(0)
                .SetMultipliedLeading(1f)
                .SetTextAlignment(TextAlignment.CENTER);
            var isMultiSigner = signers.Count > 1;
            var detailFontSize = isMultiSigner ? Math.Max(4.2f, 5.8f - (signers.Count - 1) * 0.6f) : 5.8f;
            var details = new Paragraph()
                .SetMargin(0)
                .SetMultipliedLeading(1.1f)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetFont(regularFont)
                .SetFontSize(detailFontSize)
                .SetFontColor(nameColor);

            for (var i = 0; i < signers.Count; i++)
            {
                var signer = signers[i];
                var prefix = i == 0 ? "" : "\n";
                details.Add(new Text($"{prefix}{signer.Name}").SetFont(boldFont));
                details.Add(new Text($"\n{signer.SignedAt:dd/MM/yyyy HH:mm:ss}").SetFontColor(mutedColor));
                if (!string.IsNullOrWhiteSpace(signer.CertificateSerial))
                    details.Add(new Text($"\nSerial: {signer.CertificateSerial}").SetFontColor(mutedColor).SetFontSize(Math.Max(4f, detailFontSize - 0.6f)));
            }

            using (var headerCanvas = new Canvas(pdfCanvas, headerRect))
            {
                headerCanvas.Add(title);
            }

            using (var bodyCanvas = new Canvas(pdfCanvas, bodyRect))
            {
                bodyCanvas.Add(details);
            }
        }
    }
}
