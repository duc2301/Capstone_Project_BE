using Application.DTOs.ApiResponseDTO;
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
    /// Stamp chu ky truc quan "Đã ký số" vao ban PDF goc sau khi VNPT SmartCA da ky thanh cong,
    /// tao FileVersion moi cho ban da ky va giu nguyen ban goc.
    /// </summary>
    public class PdfSignatureService : IPdfSignatureService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IFolderPermissionService _permission;
        private readonly IApprovalService _approvalService;
        private readonly ILogger<PdfSignatureService> _logger;

        public PdfSignatureService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            IFolderPermissionService permission,
            IApprovalService approvalService,
            ILogger<PdfSignatureService> logger)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _permission = permission;
            _approvalService = approvalService;
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

            if (fileItem.FileType != FileType.Pdf)
                return ApiResponse.Fail("Only PDF files support visual signature.");

            await _approvalService.RequireTeamLeaderAsync(fileItem.Id, actor);

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

            if (folder.Area != CdeArea.Wip)
                return ApiResponse.Fail("File must be in WIP to generate signed PDF.");

            if (!fileItem.RequiresSignature)
                return ApiResponse.Fail("This file does not require digital signature.");

            if (approval.Status != ApprovalRequestStatus.Pending)
                return ApiResponse.Fail("Approval request must be pending.");

            var position = (await _unitOfWork.Repository<FileSignaturePosition>().FindAsync(
                    p => p.FileItemId == fileItem.Id))
                .FirstOrDefault();
            if (position == null)
                return ApiResponse.Fail("Signature position must be set before signing.");

            var transaction = await GetLatestSignedTransactionAsync(fileItem.Id, approvalId);
            if (transaction == null)
                return ApiResponse.Fail("SmartCA signing transaction must be completed (Signed) before generating signed PDF.");

            if (!fileItem.CurrentVersionId.HasValue)
                return ApiResponse.Fail("File has no content version.");

            var currentVersion = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value);
            if (currentVersion == null)
                return ApiResponse.Fail("Current version not found.");

            FileVersion signedVersion;
            try
            {
                var signedBy = transaction.SignedBy ?? actor;
                var signedAt = transaction.SignedAt ?? DateTime.UtcNow;
                var signerAccount = await _unitOfWork.Repository<Account>().GetByIdAsync(signedBy);

                var stampedBytes = await StampSignatureAsync(
                    currentVersion.StoragePath,
                    position,
                    signerAccount?.UserName ?? signedBy.ToString(),
                    signedAt,
                    transaction.CertificateSerial,
                    transaction.TransactionId);

                using var output = new MemoryStream(stampedBytes);
                var stored = await _storage.SaveAsync(output, folder.ProjectId, fileItem.FolderId, ".pdf");

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
                    Format = "pdf",
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

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate signed PDF for approval {ApprovalId}", approvalId);
                return ApiResponse.Fail("SmartCA signed successfully but signed PDF generation failed.");
            }

            // Ky xong la dieu kien duy nhat con thieu de duyet -> tu dong approve + chuyen vung WIP -> Shared.
            // De ngoai try/catch o tren: loi o day (vd khong phai Team Leader) khong duoc bao thanh "PDF generation failed".
            await _approvalService.ApproveAsync(approvalId, actor);

            var info = await BuildSignedFileInfoAsync(fileItem, signedVersion, transaction);
            return ApiResponse.Success("Signed PDF generated, approved and moved to next zone successfully", info);
        }

        public async Task<ApiResponse> GetSignedFileInfoAsync(Guid fileItemId, Guid actor)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (fileItem == null)
                return ApiResponse.Fail("File not found.");

            //await _permission.RequireAsync(actor, fileItem.FolderId, FolderAction.Download);

            if (!fileItem.SignedVersionId.HasValue)
                return ApiResponse.Fail("Signed PDF not available.");

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
                FileName = $"{fileItem.Name}_signed.pdf",
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

        private async Task<byte[]> StampSignatureAsync(
            string storagePath,
            FileSignaturePosition position,
            string signedByName,
            DateTime signedAt,
            string? certificateSerial,
            string? transactionId)
        {
            using var inputStream = await _storage.OpenReadAsync(storagePath);
            using var outputStream = new MemoryStream();
            var reader = new PdfReader(inputStream);
            var writer = new PdfWriter(outputStream);
            writer.SetCloseStream(false); // khong de PdfDocument.Close() dong luon outputStream (MemoryStream con can doc sau)

            using (var pdfDocument = new PdfDocument(reader, writer))
            {
                var page = pdfDocument.GetPage(position.PageNumber);

                // FE luu Position.Y theo kieu man hinh/CSS (goc top-left, Y tang xuong duoi).
                // iText dung he toa do PDF chuan (goc bottom-left, Y tang len tren) -> phai quy doi truoc khi ve,
                // neu khong chu ky se bi dong dau lat nguoc theo chieu doc so voi vi tri FE da chon.
                var pageHeight = page.GetPageSize().GetHeight();
                var pdfY = pageHeight - position.Y - position.Height;

                var pdfCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
                DrawSignatureStamp(pdfCanvas, position, pdfY, signedByName, signedAt, certificateSerial, transactionId);
            }

            return outputStream.ToArray();
        }

        private static readonly Lazy<PdfFont> _regularFont = new(() => LoadEmbeddedFont("NotoSans-Regular.ttf"));
        private static readonly Lazy<PdfFont> _boldFont = new(() => LoadEmbeddedFont("NotoSans-Bold.ttf"));

        // Font Helvetica mac dinh cua iText khong co dau tieng Viet -> phai nhung font Unicode (NotoSans) rieng.
        private static PdfFont GetRegularFont() => _regularFont.Value;
        private static PdfFont GetBoldFont() => _boldFont.Value;

        private static PdfFont LoadEmbeddedFont(string fileName)
        {
            var assembly = typeof(PdfSignatureService).Assembly;
            var resourceName = $"Application.Resources.Fonts.{fileName}";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded font resource not found: {resourceName}");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return PdfFontFactory.CreateFont(buffer.ToArray(), PdfEncodings.IDENTITY_H);
        }

        // Ve khung chu ky truc quan: bo goc, dai mau tieu de "Đã ký số", noi dung chi tiet ben duoi.
        // pdfY = toa do Y theo he PDF (goc duoi-trai) da duoc quy doi tu Position.Y (FE luu kieu top-left).
        private static void DrawSignatureStamp(
            iText.Kernel.Pdf.Canvas.PdfCanvas pdfCanvas,
            FileSignaturePosition position,
            float pdfY,
            string signedByName,
            DateTime signedAt,
            string? certificateSerial,
            string? transactionId)
        {
            var accentColor = new DeviceRgb(21, 128, 61);   // green-700
            var bgColor = new DeviceRgb(240, 253, 244);      // green-50
            var textColor = new DeviceRgb(31, 41, 55);       // slate-800
            var mutedColor = new DeviceRgb(100, 116, 139);   // slate-500

            const float padding = 5f;
            var headerHeight = Math.Min(16f, position.Height * 0.3f);
            var headerRect = new Rectangle(position.X, pdfY + position.Height - headerHeight, position.Width, headerHeight);
            var bodyRect = new Rectangle(
                position.X + padding,
                pdfY + padding,
                Math.Max(1, position.Width - padding * 2),
                Math.Max(1, position.Height - headerHeight - padding * 1.5f));

            pdfCanvas.SaveState()
                .SetFillColor(bgColor)
                .SetStrokeColor(accentColor)
                .SetLineWidth(1f)
                .RoundRectangle(position.X, pdfY, position.Width, position.Height, 5)
                .FillStroke()
                .SetFillColor(accentColor)
                .RoundRectangle(headerRect.GetX(), headerRect.GetY(), headerRect.GetWidth(), headerRect.GetHeight(), 5)
                .Fill()
                .RestoreState();

            var boldFont = GetBoldFont();
            var regularFont = GetRegularFont();

            var title = new Paragraph()
                .Add(new Text("✓ ĐÃ KÝ SỐ").SetFont(boldFont).SetFontSize(8f).SetFontColor(ColorConstants.WHITE))
                .SetMargin(0)
                .SetMultipliedLeading(1f)
                .SetTextAlignment(TextAlignment.CENTER);

            var details = new Paragraph()
                .SetMargin(0)
                .SetMultipliedLeading(1.15f)
                .SetTextAlignment(TextAlignment.LEFT)
                .SetFont(regularFont)
                .SetFontSize(5.8f)
                .SetFontColor(textColor)
                .Add(new Text(signedByName).SetFont(boldFont))
                .Add(new Text($"\n{signedAt:dd/MM/yyyy HH:mm:ss}").SetFontColor(mutedColor));

            if (!string.IsNullOrWhiteSpace(certificateSerial))
                details.Add(new Text($"\nSerial: {certificateSerial}").SetFontColor(mutedColor).SetFontSize(5.2f));
            if (!string.IsNullOrWhiteSpace(transactionId))
                details.Add(new Text($"\nTxn: {transactionId}").SetFontColor(mutedColor).SetFontSize(5.2f));

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
