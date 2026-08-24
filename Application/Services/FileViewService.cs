using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Audit;
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

        private static readonly string[] TextExts = { ".txt", ".csv" };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileViewRepository _files;
        private readonly IPermissionCheckingService _permission;
        private readonly IFileStorageService _storage;
        private readonly ICdeStorageKeyBuilder _storageKey;
        private readonly IOfficeToPdfConverter _officeConverter;
        private readonly IModelTranslationQueue _translationQueue;
        private readonly ILogger<FileViewService> _logger;
        private readonly IAuditLogService _auditLog;
        private readonly IWatermarkService _watermark;

        public FileViewService(
            IUnitOfWork unitOfWork,
            IFileViewRepository files,
            IPermissionCheckingService permission,
            IFileStorageService storage,
            ICdeStorageKeyBuilder storageKey,
            IOfficeToPdfConverter officeConverter,
            IModelTranslationQueue translationQueue,
            ILogger<FileViewService> logger,
            IAuditLogService auditLog,
            IWatermarkService watermark)
        {
            _unitOfWork = unitOfWork;
            _files = files;
            _permission = permission;
            _storage = storage;
            _storageKey = storageKey;
            _officeConverter = officeConverter;
            _translationQueue = translationQueue;
            _logger = logger;
            _auditLog = auditLog;
            _watermark = watermark;
        }

        public async Task<FileViewInfoDTO> GetViewInfoAsync(Guid fileItemId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await RequireViewableFileAsync(fileItemId, actor, ct);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _files.GetVersionForUpdateAsync(fileItem.CurrentVersionId.Value, ct)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            await LogViewAsync(fileItem, version, actor);

            return await BuildViewInfoAsync(fileItem, version, ct);
        }

        public async Task<FileViewInfoDTO> GetVersionViewInfoAsync(
            Guid fileItemId, Guid versionStateId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await RequireViewableFileAsync(fileItemId, actor, ct);
            var version = await RequireVersionOfFileAsync(fileItem.Id, versionStateId, ct);

            await LogViewAsync(fileItem, version, actor);

            return await BuildViewInfoAsync(fileItem, version, ct);
        }

        // Ghi nhật ký "xem tài liệu". Gọi SAU khi đã qua kiểm quyền (RequireViewableFileAsync) để
        // không ghi lại những lượt bị từ chối.
        // Dùng LogThrottledAsync vì đây là luồng chỉ-đọc (không có transaction nghiệp vụ để bám vào)
        // và vì mở lại cùng một tệp nhiều lần không nên đẻ ra nhiều dòng log.
        // folderId là bắt buộc: bộ lọc quyền của view "/my" chạy theo FolderId.
        private async Task LogViewAsync(FileItem fileItem, FileVersionState version, Guid actor)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);

            await _auditLog.LogThrottledAsync(
                LogScope.Group, AuditAction.View, nameof(FileItem), fileItem.Id.ToString(), actor,
                detail: $"Xem '{fileItem.Name}' ({version.DisplayVersion})",
                projectId: folder?.ProjectId, folderId: fileItem.FolderId);
        }

        private async Task<FileItem> RequireViewableFileAsync(Guid fileItemId, Guid actor, CancellationToken ct)
        {
            var fileItem = await _files.GetFileItemAsync(fileItemId, ct)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.CanViewFileAsync(fileItem.Id, actor);
            return fileItem;
        }

        private async Task<FileVersionState> RequireVersionOfFileAsync(
            Guid fileItemId, Guid versionStateId, CancellationToken ct)
        {
            var version = await _files.GetVersionForUpdateAsync(versionStateId, ct)
                ?? throw new ApiExceptionResponse("Version not found.", 404);

            if (version.FileItemId != fileItemId)
                throw new ApiExceptionResponse("Version does not belong to this file.", 400);

            return version;
        }

        private async Task<FileViewInfoDTO> BuildViewInfoAsync(
            FileItem fileItem, FileVersionState version, CancellationToken ct)
        {
            var format = version.Format ?? string.Empty;
            var ext = format.StartsWith('.') ? format.ToLowerInvariant() : "." + format.ToLowerInvariant();
            var fileName = FileDownloadNaming.BuildFileName(fileItem.Name, format);
            var hasStoredContent = !string.IsNullOrWhiteSpace(version.StoragePath);

            var folder = await _files.GetFolderAsync(fileItem.FolderId, ct)
                ?? throw new ApiExceptionResponse("File folder not found.", 404);

            var info = fileItem.FileType switch
            {
                FileType.Ifc or FileType.Cad => await BuildModelAsync(version, fileName, format),
                FileType.Pdf or FileType.Image when hasStoredContent
                    => BuildInline(fileItem, version, ext, fileName, format),
                FileType.Office when hasStoredContent
                    => await BuildOfficeAsync(fileItem, version, ext, fileName, format, ct),
                _ => Download(fileName, format),
            };

            info.Area = folder.Area;
            info.FolderId = fileItem.FolderId;
            info.ProjectId = folder.ProjectId;
            info.VersionStateId = version.Id;
            info.DisplayVersion = version.DisplayVersion;
            info.IsCurrentVersion = fileItem.CurrentVersionId == version.Id;
            info.Description = version.Description;
            info.Warnning = version.Warnning;
            info.WarnningMessage = version.WarnningMessage;
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
                ViewerError = version.ViewerError,
                FileName = fileName,
                Format = format,
            };
        }

        // Dịch lại model (khi Failed, hoặc người dùng chủ động làm mới): reset trạng thái + đẩy lại vào hàng đợi nền.
        public async Task RetranslateAsync(Guid fileItemId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await _files.GetFileItemAsync(fileItemId, ct)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.CanViewFileAsync(fileItem.Id, actor);

            if (fileItem.FileType is not (FileType.Ifc or FileType.Cad))
                throw new ApiExceptionResponse("File này không phải model 3D/CAD nên không cần dịch.", 400);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _files.GetVersionForUpdateAsync(fileItem.CurrentVersionId.Value, ct)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            version.ViewerStatus = ModelViewerStatus.Pending;
            version.ViewerProgress = null;
            version.ViewerError = null;
            await _unitOfWork.CommitAsync();
            _translationQueue.Enqueue(version.Id);
        }

        // ---- PDF/ảnh/text: trỏ FE tới endpoint proxy same-origin (/view-content) thay vì presigned URL public ----
        // Không còn phát hành link S3 công khai: nội dung chỉ chảy qua API có [Authorize] + kiểm quyền theo từng request,
        // nên URL dán sang trình duyệt khác (không JWT) sẽ 401. FE fetch URL này kèm Bearer (như /view-pdf) rồi render.
        private FileViewInfoDTO BuildInline(
            FileItem fileItem, FileVersionState version, string ext, string fileName, string format)
            => new()
            {
                Kind = KindInline,
                Url = BuildContentUrl(fileItem.Id, version.Id),
                ContentType = _storage.GetContentType(ext),
                FileName = fileName,
                Format = format,
            };

        // ---- Office: txt/csv xem text; doc/xls/ppt convert sang PDF rồi inline ----
        private async Task<FileViewInfoDTO> BuildOfficeAsync(
            FileItem fileItem, FileVersionState version, string ext, string fileName, string format, CancellationToken ct)
        {
            if (TextExts.Contains(ext))
                return BuildInline(fileItem, version, ext, fileName, format);

            // doc/xls/ppt: phải convert được sang PDF mới xem inline được; không thì tải về.
            var previewPath = await EnsureOfficePdfPathAsync(fileItem, version, ext, ct);
            if (string.IsNullOrWhiteSpace(previewPath))
                return Download(fileName, format);

            return new FileViewInfoDTO
            {
                Kind = KindInline,
                Url = BuildContentUrl(fileItem.Id, version.Id),
                ContentType = "application/pdf",
                FileName = fileName,
                Format = format,
            };
        }

        // Đường dẫn same-origin để FE xem nội dung (fetch kèm Bearer rồi render). Ghim versionStateId để xem đúng
        // phiên bản đang mở (kể cả bản cũ) và không phụ thuộc CurrentVersionId có đổi giữa lúc mở view và lúc tải bytes.
        private static string BuildContentUrl(Guid fileItemId, Guid versionStateId)
            => $"/api/file-items/{fileItemId}/view-content?versionStateId={versionStateId}";

        // Đảm bảo có bản PDF của file Office (convert + cache PreviewStoragePath 1 lần). null nếu không convert được.
        private async Task<string?> EnsureOfficePdfPathAsync(
            FileItem fileItem, FileVersionState version, string ext, CancellationToken ct)
        {
            if (!_officeConverter.CanConvert(ext))
                return null;

            if (!string.IsNullOrWhiteSpace(version.PreviewStoragePath))
                return version.PreviewStoragePath;

            var folder = await _files.GetFolderAsync(fileItem.FolderId, ct)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            try
            {
                await using var source = await _storage.OpenReadAsync(version.StoragePath!, ct);
                await using var pdf = await _officeConverter.ConvertToPdfAsync(source, ext, ct);
                var objectName = await _storageKey.ForDerivedAsync(folder.Id, DerivedFileKind.Preview, ".pdf", ct);
                var stored = await _storage.SaveAsync(pdf, objectName, ct);

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
        public async Task<InlinePdfResult> OpenViewPdfAsync(Guid fileItemId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await _files.GetFileItemAsync(fileItemId, ct)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.CanViewFileAsync(fileItem.Id, actor);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _files.GetVersionForUpdateAsync(fileItem.CurrentVersionId.Value, ct)
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

            // Watermark giống /view-content và /download — tránh mở tool markup này để né watermark.
            var folder = await _files.GetFolderAsync(fileItem.FolderId, ct);
            var watermarked = await _watermark.ApplyAsync(stream, "pdf", folder?.Area, actor, ct);
            if (!ReferenceEquals(watermarked, stream))
            {
                await stream.DisposeAsync();
                stream = watermarked;
            }

            return new InlinePdfResult(stream, $"{fileItem.Name}.pdf");
        }

        // ---- Proxy nội dung xem inline (thay presigned URL public) — same-origin, kiểm quyền lại theo TỪNG request ----
        //  PDF/ảnh -> file gốc; txt/csv -> file gốc; doc/xls/ppt -> bản convert PDF (dùng lại cache PreviewStoragePath).
        //  Chảy qua API có [Authorize] + CanViewFile nên URL dán sang trình duyệt khác (không JWT) -> 401; mất quyền -> 403.
        public async Task<InlineContentResult> OpenViewContentAsync(
            Guid fileItemId, Guid? versionStateId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await RequireViewableFileAsync(fileItemId, actor, ct);

            FileVersionState version;
            if (versionStateId.HasValue)
            {
                version = await RequireVersionOfFileAsync(fileItem.Id, versionStateId.Value, ct);
            }
            else
            {
                if (!fileItem.CurrentVersionId.HasValue)
                    throw new ApiExceptionResponse("File has no content version.", 404);
                version = await _files.GetVersionForUpdateAsync(fileItem.CurrentVersionId.Value, ct)
                    ?? throw new ApiExceptionResponse("Current version not found.", 404);
            }

            if (string.IsNullOrWhiteSpace(version.StoragePath))
                throw new ApiExceptionResponse("Version has no stored content.", 404);

            var format = version.Format ?? string.Empty;
            var ext = format.StartsWith('.') ? format.ToLowerInvariant() : "." + format.ToLowerInvariant();
            var fileName = FileDownloadNaming.BuildFileName(fileItem.Name, format);

            string storagePath;
            string contentType;
            switch (fileItem.FileType)
            {
                case FileType.Pdf:
                case FileType.Image:
                    storagePath = version.StoragePath!;
                    contentType = _storage.GetContentType(ext);
                    break;

                // txt/csv: xem thẳng nội dung text gốc.
                case FileType.Office when TextExts.Contains(ext):
                    storagePath = version.StoragePath!;
                    contentType = _storage.GetContentType(ext);
                    break;

                // doc/xls/ppt: phục vụ bản PDF đã convert (đã cache lúc gọi /view -> ở đây chỉ đọc lại cache).
                case FileType.Office:
                    storagePath = await EnsureOfficePdfPathAsync(fileItem, version, ext, ct)
                        ?? throw new ApiExceptionResponse("Không thể chuyển file sang PDF để xem.", 415);
                    contentType = "application/pdf";
                    break;

                default:
                    throw new ApiExceptionResponse("File này không hỗ trợ xem trực tiếp.", 400);
            }

            var stream = await _storage.OpenReadAsync(storagePath, ct);

            // Watermark cả lúc xem online, không chỉ lúc tải — ảnh/txt/csv thì bỏ qua vì không hỗ trợ.
            if (contentType == "application/pdf")
            {
                var folder = await _files.GetFolderAsync(fileItem.FolderId, ct);
                var watermarked = await _watermark.ApplyAsync(stream, "pdf", folder?.Area, actor, ct);
                if (!ReferenceEquals(watermarked, stream))
                {
                    await stream.DisposeAsync();
                    stream = watermarked;
                }
            }

            return new InlineContentResult(stream, contentType, fileName);
        }

        private static FileViewInfoDTO Download(string fileName, string format) => new()
        {
            Kind = KindDownload,
            FileName = fileName,
            Format = format,
        };
    }
}
