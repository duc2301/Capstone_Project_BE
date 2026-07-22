using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.RequestDTOs.FileVersion;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileVersion;
using Application.DTOs.ResponseDTOs.NamingConvention;
using Application.ExceptionMiddleware;
using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Domain.Enum.Loi;

namespace Application.Services
{
    public class FileUploadService : IFileUploadService
    {
        private static readonly char[] IllegalNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        // [CÔNG TẮC DEMO] Tự động biên dịch model IFC/CAD lên Autodesk APS NGAY khi upload (lưu sẵn ViewerUrn
        // để lúc mở "Xem chi tiết" không phải chờ dịch).
        //  - false (mặc định hiện tại): TẮT để khỏi ngốn dung lượng Autodesk (gói free) — nếu mọi model upload
        //    đều dịch & lưu trên APS thì rất nhanh hết quota. Model chỉ được dịch ON-DEMAND lúc người dùng lần đầu
        //    mở "Xem chi tiết" (xem FileViewService.BuildModelAsync: ViewerStatus = None -> tự đẩy vào hàng đợi).
        //  - true: BẬT lại khi DEMO với giáo viên để model dịch sẵn từ lúc upload, mở xem là có ngay (đỡ phải chờ).
        private static readonly bool AutoTranslateModelsOnUpload = false;

        private readonly IUnitOfWork _unitOfWork;
        //private readonly IFolderPermissionServiceOld _permission;
        private readonly IFileStorageService _storage;
        private readonly IModelTranslationQueue _translationQueue;
        private readonly ILoiCheckQueue _loiCheckQueue;
        private readonly IMapper _mapper;
        private readonly INamingConventionService _naming;
        private readonly INameMatchContentBackgroundService _nameMatchContentBackgroundService;
        private readonly IFileVersionService _fileVersionService;
        private readonly IFileLinkService _fileLink;

        public FileUploadService(IUnitOfWork unitOfWork, IFileStorageService storage, IModelTranslationQueue translationQueue, ILoiCheckQueue loiCheckQueue, IMapper mapper, INamingConventionService naming, INameMatchContentBackgroundService nameMatchContentBackgroundService, IFileVersionService fileVersionService, IFileLinkService fileLink)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _translationQueue = translationQueue;
            _loiCheckQueue = loiCheckQueue;
            _mapper = mapper;
            _naming = naming;
            _nameMatchContentBackgroundService = nameMatchContentBackgroundService;
            _fileVersionService = fileVersionService;
            _fileLink = fileLink;
        }

        public async Task<FileUploadResultDTO> UploadAsync(
            UploadFileDTO dto, Stream content, string originalFileName, Guid actor, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(dto.FolderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            // ④ Consistency: chỉ được upload vào WIP/Shared (Published/Archived là khu xuất bản/lưu trữ).
            if (folder.Area is CdeArea.Published or CdeArea.Archived)
                throw new ApiExceptionResponse(
                    "Không thể tải file trực tiếp lên thư mục Published/Archived. Tải lên WIP hoặc Shared thay thế.", 400);
            if (folder.ParentFolderId == null)
                throw new ApiExceptionResponse(
                    "Không thể tải file trực tiếp lên thư mục gốc. Tạo thư mục con để upload thay thế.", 400);

            var name = string.IsNullOrWhiteSpace(dto.Name)
                ? Path.GetFileNameWithoutExtension(originalFileName)
                : dto.Name.Trim();
            var ext = Path.GetExtension(originalFileName);
            var naming = dto.BypassNamingConvention
                ? new FileNameGenerationResultDTO { HasNamingConvention = false }
                : await _naming.GenerateFileNameAsync(dto.FolderId, dto.NamingSelections, originalFileName, ct);
            if (naming.HasNamingConvention)
                name = naming.FileNameWithoutExtension;

            // ③ Kiểm tra tên file (rule mặc định: không rỗng, không ký tự cấm, có đuôi).
            ValidateName(name);
            if (string.IsNullOrWhiteSpace(ext))
                throw new ApiExceptionResponse("File must have an extension.", 400);

            // ④ Đuôi file phải khớp FileType khai báo.
            ValidateExtensionMatchesType(ext, dto.FileType);

            // ① Đối chiếu quyền: file mới cần Edit.
            //await _permission.RequireAsync(actor, folder.Id, FolderAction.Edit);

            // ② Tệp liên quan: KIỂM phạm vi TRƯỚC khi lưu file — id sai/ngoài phạm vi thì fail ở đây,
            // chưa lưu byte nào (hệ versioning mới commit FileItem giữa luồng, không thể rollback file mồ côi).
            if (dto.RelatedFileItemIds is { Count: > 0 })
                await _fileLink.ValidateUploadLinkTargetsAsync(folder, dto.RelatedFileItemIds, actor, isSystemAdmin, ct);

            // ⑦ Lưu nội dung file (đĩa local).
            var stored = await _storage.SaveAsync(content, folder.ProjectId, folder.Id, ext, ct);
            var url = await _storage.GetPresignedUrlAsync(stored.RelativePath, 60, ct);
            var now = DateTime.UtcNow;
            var format = ext.TrimStart('.').ToLowerInvariant();

            // ⑤ Versioning: trùng tên trong folder = upload thay thế (WorkingVersion +1);
            // tên mới = tài liệu mới (P01.01). Toàn bộ quy tắc nằm trong FileVersionService.
            var fileData = new FileVersionDataDTO
            {
                StoragePath = stored.RelativePath,
                FileSizeBytes = stored.SizeBytes,
                Format = format,
                Checksum = stored.Checksum,
                UploadedByAccountId = actor,
                // Model IFC/CAD: chỉ đánh dấu chờ dịch nền khi BẬT công tắc tự dịch khi upload.
                ViewerStatus = AutoTranslateModelsOnUpload && IsModelType(dto.FileType)
                    ? ModelViewerStatus.Pending
                    : ModelViewerStatus.None
            };

            FileVersionResult version;
            FileItem fileItem;
            try
            {
                version = await _fileVersionService.GetNextUploadVersionAsync(folder.Id, name, fileData);
            }
            catch (InvalidOperationException ex)
            {
                // vd: tài liệu đang Published — không nhận upload thay thế.
                throw new ApiExceptionResponse(ex.Message, 409);
            }

            if (version.IsNewDocument)
            {
                fileItem = new FileItem
                {
                    Id = Guid.NewGuid(),
                    FolderId = folder.Id,
                    Name = name,
                    FileType = dto.FileType,
                    CreatedByAccountId = actor,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _unitOfWork.Repository<FileItem>().CreateAsync(fileItem);
                // Naming convention: lưu breakdown từng segment của tên — chỉ khi tài liệu MỚI
                // (upload thay thế trùng tên = cùng bộ giá trị, metadata giữ nguyên).
                // Không SaveChanges ở đây: commit chung 1 lần ở cuối flow.
                if (naming.HasNamingConvention)
                    await _naming.StageFileNamingMetadataAsync(fileItem.Id, naming);
                // FileItem đang tracked (Added) nên versioning service nhìn thấy ngay qua cùng DbContext.
                version = await _fileVersionService.CreateInitialVersionAsync(fileItem.Id, fileData);
            }
            else
            {
                fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(version.FileItemId!.Value)
                    ?? throw new ApiExceptionResponse("File not found.", 404);
                fileItem.FileType = dto.FileType;
                fileItem.UpdatedAt = now;
            }

            // CurrentVersionId giờ trỏ sang dòng FileVersionStates hiện hành (hệ versioning mới).
            fileItem.CurrentVersionId = version.VersionStateId;

            // ② Tệp liên quan (tùy chọn): stage row link cho CẢ file mới lẫn upload thay thế (fileItem đã có ở đây).
            // Đã validate scope từ đầu flow (trước khi lưu file) nên tới đây chỉ tạo row, commit chung ở dưới.
            await StageRelatedFileLinksAsync(fileItem.Id, folder, dto, actor, isSystemAdmin);

            // [TẠM TẮT] Cổng kiểm LOI (advisory) — chức năng chưa hoàn thiện. Mở lại cả khối này khi xong.
            // if (dto.FileType == FileType.Ifc)
            //     await _unitOfWork.Repository<FileVersionLoiCheck>().CreateAsync(NewLoiPending(version.VersionStateId!.Value, now));
            await _unitOfWork.CommitAsync();

            if (AutoTranslateModelsOnUpload && IsModelType(dto.FileType))
                _translationQueue.Enqueue(version.VersionStateId!.Value);
            // [TẠM TẮT] LOI check queue — mở lại khi chức năng hoàn thiện.
            // if (dto.FileType == FileType.Ifc)
            //     _loiCheckQueue.Enqueue(version.VersionStateId!.Value);
            _nameMatchContentBackgroundService.Enqueue(fileItem.Id);

            return new FileUploadResultDTO
            {
                FileItem = _mapper.Map<FileItemResponseDTO>(fileItem),
                Version = new FileVersionResponseDTO
                {
                    Id = version.VersionStateId!.Value,
                    FileItemId = fileItem.Id,
                    VersionNumber = version.WorkingVersion,
                    DisplayVersion = version.DisplayVersion,
                    StoragePath = stored.RelativePath,
                    FileSizeBytes = stored.SizeBytes,
                    Format = format,
                    Checksum = stored.Checksum,
                    IsHidden = false,
                    UploadedByAccountId = actor,
                    UploadedAt = now
                },
                Url = url
            };
        }

        public async Task<DownloadFileResult> OpenDownloadAsync(Guid fileItemId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            //await _permission.RequireAsync(actor, fileItem.FolderId, FolderAction.Download);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            if (string.IsNullOrEmpty(version.StoragePath) || string.IsNullOrEmpty(version.Format))
                throw new ApiExceptionResponse("Current version has no stored content.", 404);

            var stream = await _storage.OpenReadAsync(version.StoragePath, ct);
            var downloadName = $"{fileItem.Name}.{version.Format}";
            return new DownloadFileResult(stream, downloadName, _storage.GetContentType(version.Format));
        }

        public async Task<string?> GetViewUrlAsync(Guid fileItemId, Guid actor, int minutes = 60, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            //await _permission.RequireAsync(actor, fileItem.FolderId, FolderAction.Download);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            if (string.IsNullOrEmpty(version.StoragePath))
                throw new ApiExceptionResponse("Current version has no stored content.", 404);

            return await _storage.GetPresignedUrlAsync(version.StoragePath, minutes, ct);
        }

        // ---------- nội bộ ----------

        private async Task StageRelatedFileLinksAsync(
            Guid fileItemId, Folder targetFolder, UploadFileDTO dto, Guid actor, bool isSystemAdmin)
        {
            if (dto.RelatedFileItemIds is not { Count: > 0 }) return;

            await _fileLink.StageLinksOnUploadAsync(
                fileItemId, targetFolder, dto.RelatedFileItemIds, actor, isSystemAdmin);
        }

        // Chỉ model IFC/CAD mới cần dịch lên APS (xem ModelTranslationWorker).
        private static bool IsModelType(FileType type) => type is FileType.Ifc or FileType.Cad;

        // Bản ghi kiểm LOI ở trạng thái chờ (để FE hiện "đang kiểm" ngay sau upload .ifc).
        private static FileVersionLoiCheck NewLoiPending(Guid fileVersionId, DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            FileVersionId = fileVersionId,
            Status = LoiCheckStatus.Pending,
            Verdict = LoiVerdict.None,
            CreatedAt = now,
            UpdatedAt = now
        };

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ApiExceptionResponse("Tên file là bắt buộc.", 400);
            if (name.IndexOfAny(IllegalNameChars) >= 0)
                throw new ApiExceptionResponse("Tên file chứa ký tự không hợp lệ ( \\ / : * ? \" < > | ).", 400);
            if (name.Length > 200)
                throw new ApiExceptionResponse("Tên file quá dài (tối đa 200 ký tự).", 400);
        }

        private static void ValidateExtensionMatchesType(string ext, FileType type)
        {
            ext = ext.Trim().ToLowerInvariant();
            var allowed = type switch
            {
                FileType.Pdf => new[] { ".pdf" },
                FileType.Ifc => new[] { ".ifc" },
                FileType.Image => new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" },
                FileType.Cad => new[] { ".dwg", ".dxf", ".rvt", ".nwc", ".nwd", ".dgn" },
                FileType.Office => new[] { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".txt" },
                FileType.Other => Array.Empty<string>(),
                _ => Array.Empty<string>()
            };

            if (allowed.Length > 0 && !allowed.Contains(ext))
                throw new ApiExceptionResponse(
                    $"Extension '{ext}' does not match file type '{type}'. Allowed: {string.Join(", ", allowed)}.", 400);
        }
    }
}
