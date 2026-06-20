using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
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
        private readonly ICurrentUserService _currentUser;
        private readonly IFolderPermissionService _permission;
        private readonly IFileStorageService _storage;
        private readonly IOfficeToPdfConverter _officeConverter;
        private readonly IModelTranslationQueue _translationQueue;
        private readonly ILogger<FileViewService> _logger;

        public FileViewService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IFolderPermissionService permission,
            IFileStorageService storage,
            IOfficeToPdfConverter officeConverter,
            IModelTranslationQueue translationQueue,
            ILogger<FileViewService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _permission = permission;
            _storage = storage;
            _officeConverter = officeConverter;
            _translationQueue = translationQueue;
            _logger = logger;
        }

        public async Task<FileViewInfoDTO> GetViewInfoAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            // Xem nội dung = mức quyền Download (đồng nhất với OpenDownloadAsync/GetViewUrlAsync).
            await _permission.RequireAsync(actor, fileItem.FolderId, FolderAction.Download);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            var format = version.Format ?? string.Empty;
            var ext = format.StartsWith('.') ? format.ToLowerInvariant() : "." + format.ToLowerInvariant();
            var fileName = $"{fileItem.Name}.{format}";

            return fileItem.FileType switch
            {
                FileType.Ifc or FileType.Cad => await BuildModelAsync(version, fileName, format),
                FileType.Pdf or FileType.Image => await BuildInlineAsync(version.StoragePath, ext, fileName, format, ct),
                FileType.Office => await BuildOfficeAsync(fileItem, version, ext, fileName, format, ct),
                _ => Download(fileName, format),
            };
        }

        // ---- Thiết kế (IFC/CAD): dịch APS chạy NỀN (ModelTranslationWorker). /view KHÔNG chặn -> chỉ phản ánh trạng thái.
        //  Ready  -> trả Urn để FE mở viewer ngay.
        //  Pending/Processing -> trả trạng thái + tiến độ để FE hiện "đang xử lý" và poll lại.
        //  Failed -> FE báo lỗi + cho dịch lại (RetranslateAsync).
        //  None   -> file cũ (trước khi có dịch nền) hoặc chưa có job -> fallback: đẩy vào hàng đợi ngay.
        private async Task<FileViewInfoDTO> BuildModelAsync(FileVersion version, string fileName, string format)
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
        public async Task RetranslateAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.RequireAsync(actor, fileItem.FolderId, FolderAction.Download);

            if (fileItem.FileType is not (FileType.Ifc or FileType.Cad))
                throw new ApiExceptionResponse("File này không phải model 3D/CAD nên không cần dịch.", 400);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value)
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
            FileItem fileItem, FileVersion version, string ext, string fileName, string format, CancellationToken ct)
        {
            if (TextExts.Contains(ext))
                return await BuildInlineAsync(version.StoragePath, ext, fileName, format, ct);

            if (!_officeConverter.CanConvert(ext))
                return Download(fileName, format);

            if (string.IsNullOrWhiteSpace(version.PreviewStoragePath))
            {
                var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId)
                    ?? throw new ApiExceptionResponse("Folder not found.", 404);

                try
                {
                    await using var source = await _storage.OpenReadAsync(version.StoragePath, ct);
                    await using var pdf = await _officeConverter.ConvertToPdfAsync(source, ext, ct);
                    var stored = await _storage.SaveAsync(pdf, folder.ProjectId, folder.Id, ".pdf", ct);

                    version.PreviewStoragePath = stored.RelativePath;   // cache: convert 1 lần
                    await _unitOfWork.CommitAsync();
                }
                catch (Exception ex)
                {
                    // Convert thất bại (vd thiếu Syncfusion license, file hỏng) -> cho tải về thay vì 500.
                    _logger.LogWarning(ex, "Office->PDF conversion failed for file {FileName}; falling back to download.", fileName);
                    return Download(fileName, format);
                }
            }

            var url = await _storage.GetPresignedUrlAsync(version.PreviewStoragePath, UrlExpiryMinutes, ct);
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

        private static FileViewInfoDTO Download(string fileName, string format) => new()
        {
            Kind = KindDownload,
            FileName = fileName,
            Format = format,
        };
    }
}
