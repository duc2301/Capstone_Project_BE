using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    // Định tuyến "Xem chi tiết" theo FileType:
    //  - Ifc/Cad  -> "model"   : đảm bảo đã dịch APS (lưu ViewerUrn), trả Urn cho ModelViewer.
    //  - Pdf/Image-> "inline"  : presigned URL + ContentType (trình duyệt render thẳng).
    //  - Office   -> txt/csv inline text; doc/xls/ppt convert sang PDF (cache PreviewStoragePath) rồi inline.
    //  - còn lại  -> "download".
    public class FileViewService : IFileViewService
    {
        private const string KindModel = "model";
        private const string KindInline = "inline";
        private const string KindDownload = "download";
        private const int UrlExpiryMinutes = 60;

        private static readonly string[] TextExts = { ".txt", ".csv" };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderTreeRepository _folderTree;
        private readonly IPermissionCheckingRepository _permissionRepo;
        private readonly IFileStorageService _storage;
        private readonly IOfficeToPdfConverter _officeConverter;
        private readonly IModelTranslationQueue _translationQueue;
        private readonly ILogger<FileViewService> _logger;

        public FileViewService(
            IUnitOfWork unitOfWork,
            IFolderTreeRepository folderTree,
            IPermissionCheckingRepository permissionRepo,
            IFileStorageService storage,
            IOfficeToPdfConverter officeConverter,
            IModelTranslationQueue translationQueue,
            ILogger<FileViewService> logger)
        {
            _unitOfWork = unitOfWork;
            _folderTree = folderTree;
            _permissionRepo = permissionRepo;
            _storage = storage;
            _officeConverter = officeConverter;
            _translationQueue = translationQueue;
            _logger = logger;
        }

        private async Task RequireCanViewAsync(FileItem fileItem, Guid actorId, bool isSystemAdmin)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            var allowed = isSystemAdmin
                || await _folderTree.HasFullAccessAsync(folder.ProjectId, actorId)
                || (await _permissionRepo.GetUserFolderPermissionAsync(folder.Id, actorId)) is { CanView: true };

            if (!allowed)
                throw new ApiExceptionResponse("Bạn không có quyền xem file này.", 403);
        }

        public async Task<FileViewInfoDTO> GetViewInfoAsync(Guid fileItemId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await RequireCanViewAsync(fileItem, actor, isSystemAdmin);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            var format = version.Format ?? string.Empty;
            var ext = format.StartsWith('.') ? format.ToLowerInvariant() : "." + format.ToLowerInvariant();
            var fileName = $"{fileItem.Name}.{format}";

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            var info = fileItem.FileType switch
            {
                FileType.Ifc or FileType.Cad => await BuildModelAsync(version, fileName, format),
                FileType.Pdf or FileType.Image => await BuildInlineAsync(version.StoragePath!, ext, fileName, format, ct),
                FileType.Office => await BuildOfficeAsync(fileItem, version, ext, fileName, format, ct),
                _ => Download(fileName, format),
            };
            info.Area = folder.Area;
            return info;
        }

        // ---- Thiết kế (IFC/CAD): dịch APS chạy NỀN (ModelTranslationWorker). /view KHÔNG chặn -> chỉ phản ánh trạng thái.
        //  Ready  -> trả Urn để FE mở viewer ngay.
        //  Pending/Processing -> trả trạng thái + tiến độ để FE hiện "đang xử lý" và poll lại.
        //  Failed -> FE báo lỗi + cho dịch lại (RetranslateAsync).
        //  None   -> file cũ (trước khi có dịch nền) hoặc chưa có job -> fallback: đẩy vào hàng đợi ngay.
        private async Task<FileViewInfoDTO> BuildModelAsync(FileVersionState version, string fileName, string format)
        {
            var needsEnqueue = version.ViewerStatus == ModelViewerStatus.None
                || (version.ViewerStatus == ModelViewerStatus.Ready && string.IsNullOrWhiteSpace(version.ViewerUrn));

            if (needsEnqueue)
            {
                version.ViewerStatus = ModelViewerStatus.Pending;   // entity được track -> mutate trực tiếp
                version.ViewerError = null;
                await _unitOfWork.CommitAsync();
                _translationQueue.Enqueue(version.Id);
            }

            return new FileViewInfoDTO
            {
                Kind = KindModel,
                Urn = version.ViewerUrn,                 // có thể đã có (Processing/Ready) hoặc null (Pending)
                ViewerStatus = version.ViewerStatus,
                ViewerProgress = version.ViewerProgress,
                FileName = fileName,
                Format = format,
            };
        }

        // Dịch lại model (khi Failed, hoặc người dùng chủ động làm mới): reset trạng thái + đẩy lại vào hàng đợi nền.
        public async Task RetranslateAsync(Guid fileItemId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await RequireCanViewAsync(fileItem, actor, isSystemAdmin);

            if (fileItem.FileType is not (FileType.Ifc or FileType.Cad))
                throw new ApiExceptionResponse("File này không phải model 3D/CAD nên không cần dịch.", 400);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            version.ViewerStatus = ModelViewerStatus.Pending;
            version.ViewerProgress = null;
            version.ViewerError = null;
            await _unitOfWork.CommitAsync();
            _translationQueue.Enqueue(version.Id);
        }

        // ---- PDF/ảnh/text: presigned URL + content type ----
        private async Task<FileViewInfoDTO> BuildInlineAsync(
            string storagePath, string ext, string fileName, string format, CancellationToken ct)
        {
            var url = await _storage.GetPresignedUrlAsync(storagePath, UrlExpiryMinutes, ct);
            if (string.IsNullOrWhiteSpace(url))
                return Download(fileName, format);   // provider không hỗ trợ link (vd local) -> tải về

            return new FileViewInfoDTO
            {
                Kind = KindInline,
                Url = url,
                ContentType = _storage.GetContentType(ext),
                FileName = fileName,
                Format = format,
            };
        }

        // ---- Office: txt/csv xem text; doc/xls/ppt convert sang PDF rồi inline ----
        private async Task<FileViewInfoDTO> BuildOfficeAsync(
            FileItem fileItem, FileVersionState version, string ext, string fileName, string format, CancellationToken ct)
        {
            if (TextExts.Contains(ext))
                return await BuildInlineAsync(version.StoragePath!, ext, fileName, format, ct);

            var previewPath = await EnsureOfficePdfPathAsync(fileItem, version, ext, ct);
            if (string.IsNullOrWhiteSpace(previewPath))
                return Download(fileName, format);

            var url = await _storage.GetPresignedUrlAsync(previewPath, UrlExpiryMinutes, ct);
            if (string.IsNullOrWhiteSpace(url))
                return Download(fileName, format);

            return new FileViewInfoDTO
            {
                Kind = KindInline,
                Url = url,
                ContentType = "application/pdf",
                FileName = fileName,
                Format = format,
            };
        }

        // Đảm bảo có bản PDF của file Office (convert + cache PreviewStoragePath 1 lần). null nếu không convert được.
        private async Task<string?> EnsureOfficePdfPathAsync(
            FileItem fileItem, FileVersionState version, string ext, CancellationToken ct)
        {
            if (!_officeConverter.CanConvert(ext))
                return null;

            if (!string.IsNullOrWhiteSpace(version.PreviewStoragePath))
                return version.PreviewStoragePath;

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            try
            {
                await using var source = await _storage.OpenReadAsync(version.StoragePath!, ct);
                await using var pdf = await _officeConverter.ConvertToPdfAsync(source, ext, ct);
                var stored = await _storage.SaveAsync(pdf, folder.ProjectId, folder.Id, ".pdf", ct);

                version.PreviewStoragePath = stored.RelativePath;   // cache: convert 1 lần
                await _unitOfWork.CommitAsync();
                return version.PreviewStoragePath;
            }
            catch (Exception ex)
            {
                // Convert thất bại (vd thiếu Syncfusion license, file hỏng) -> null để caller fallback.
                _logger.LogWarning(ex, "Office->PDF conversion failed for file {FileName}.", $"{fileItem.Name}.{version.Format}");
                return null;
            }
        }

        // ---- Bytes PDF hiệu dụng cho pdf.js (markup 2D) — same-origin, né CORS presigned + hết hạn URL ----
        public async Task<InlinePdfResult> OpenViewPdfAsync(Guid fileItemId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await RequireCanViewAsync(fileItem, actor, isSystemAdmin);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            var format = version.Format ?? string.Empty;
            var ext = format.StartsWith('.') ? format.ToLowerInvariant() : "." + format.ToLowerInvariant();

            string storagePath;
            if (fileItem.FileType == FileType.Pdf)
            {
                storagePath = version.StoragePath!;
            }
            else if (fileItem.FileType == FileType.Office && !TextExts.Contains(ext))
            {
                storagePath = await EnsureOfficePdfPathAsync(fileItem, version, ext, ct)
                    ?? throw new ApiExceptionResponse("Không thể chuyển file sang PDF để markup.", 415);
            }
            else
            {
                throw new ApiExceptionResponse("File này không phải PDF/Office nên không hỗ trợ markup theo trang.", 400);
            }

            var stream = await _storage.OpenReadAsync(storagePath, ct);
            return new InlinePdfResult(stream, $"{fileItem.Name}.pdf");
        }

        private static FileViewInfoDTO Download(string fileName, string format) => new()
        {
            Kind = KindDownload,
            FileName = fileName,
            Format = format,
        };
    }
}
