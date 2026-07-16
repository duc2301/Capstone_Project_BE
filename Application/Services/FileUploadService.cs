using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
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
        private readonly INameMatchContentBackgroundService _nameMatchContentBackgroundService;
        private readonly IFileLinkService _fileLink;

        public FileUploadService(IUnitOfWork unitOfWork, IFileStorageService storage, IModelTranslationQueue translationQueue, ILoiCheckQueue loiCheckQueue, IMapper mapper, INameMatchContentBackgroundService nameMatchContentBackgroundService, IFileLinkService fileLink)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _translationQueue = translationQueue;
            _loiCheckQueue = loiCheckQueue;
            _mapper = mapper;
            _nameMatchContentBackgroundService = nameMatchContentBackgroundService;
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

            // ③ Kiểm tra tên file (rule mặc định: không rỗng, không ký tự cấm, có đuôi).
            ValidateName(name);
            if (string.IsNullOrWhiteSpace(ext))
                throw new ApiExceptionResponse("File must have an extension.", 400);

            // ④ Đuôi file phải khớp FileType khai báo.
            ValidateExtensionMatchesType(ext, dto.FileType);

            // ⑤ Trùng tên trong folder -> đây là phiên bản mới.
            var siblings = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => f.FolderId == folder.Id))
                .ToList();
            var existing = siblings.FirstOrDefault(
                f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            var isNewVersion = existing != null;

            // ① Đối chiếu quyền: phiên bản mới cần Update, file mới cần Edit.
            //await _permission.RequireAsync(actor, folder.Id, isNewVersion ? FolderAction.Update : FolderAction.Edit);

            // ⑦ Lưu nội dung file (đĩa local).
            var stored = await _storage.SaveAsync(content, folder.ProjectId, folder.Id, ext, ct);
            var url = await _storage.GetPresignedUrlAsync(stored.RelativePath, 60, ct);
            var now = DateTime.UtcNow;
            var format = ext.TrimStart('.').ToLowerInvariant();

            if (!isNewVersion)
            {
                var fileItem = new FileItem
                {
                    Id = Guid.NewGuid(),
                    FolderId = folder.Id,
                    Name = name,
                    FileType = dto.FileType,
                    CreatedByAccountId = actor,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                var v1 = NewVersion(fileItem.Id, 1, stored, format, actor, now);
                fileItem.CurrentVersionId = v1.Id;
                // Model IFC/CAD: chỉ đánh dấu chờ dịch nền khi BẬT công tắc tự dịch khi upload (xem AutoTranslateModelsOnUpload).
                if (AutoTranslateModelsOnUpload && IsModelType(dto.FileType))
                    v1.ViewerStatus = ModelViewerStatus.Pending;

                await _unitOfWork.Repository<FileItem>().CreateAsync(fileItem);
                await _unitOfWork.Repository<FileVersion>().CreateAsync(v1);
                // Cổng kiểm LOI (advisory): file .ifc -> tạo bản ghi Pending để FE hiện "đang kiểm".
                if (dto.FileType == FileType.Ifc)
                    await _unitOfWork.Repository<FileVersionLoiCheck>().CreateAsync(NewLoiPending(v1.Id, now));
                await StageRelatedFileLinksAsync(fileItem.Id, folder, dto, actor, isSystemAdmin);
                await _unitOfWork.CommitAsync();

                if (AutoTranslateModelsOnUpload && IsModelType(dto.FileType))
                    _translationQueue.Enqueue(v1.Id);
                if (dto.FileType == FileType.Ifc)
                    _loiCheckQueue.Enqueue(v1.Id);
                _nameMatchContentBackgroundService.Enqueue(fileItem.Id);

                return new FileUploadResultDTO
                {
                    FileItem = _mapper.Map<FileItemResponseDTO>(fileItem),
                    Version = _mapper.Map<FileVersionResponseDTO>(v1),
                    IsNewVersion = false,
                    Url = url
                };
            }

            // --- Phiên bản mới của file đã tồn tại ---
            var versions = (await _unitOfWork.Repository<FileVersion>()
                    .FindAsync(v => v.FileItemId == existing!.Id))
                .ToList();
            var nextNo = (versions.Count == 0 ? 0 : versions.Max(v => v.VersionNumber)) + 1;

            var oldVersion = versions.FirstOrDefault(v => v.Id == existing!.CurrentVersionId)
                             ?? versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

            var newVersion = NewVersion(existing!.Id, nextNo, stored, format, actor, now);
            if (AutoTranslateModelsOnUpload && IsModelType(dto.FileType))
                newVersion.ViewerStatus = ModelViewerStatus.Pending;
            await _unitOfWork.Repository<FileVersion>().CreateAsync(newVersion);
            if (dto.FileType == FileType.Ifc)
                await _unitOfWork.Repository<FileVersionLoiCheck>().CreateAsync(NewLoiPending(newVersion.Id, now));

            existing.CurrentVersionId = newVersion.Id;   // entity được track -> mutate trực tiếp
            existing.UpdatedAt = now;

            Guid? archivedFileItemId = null;
            if (oldVersion != null)
            {
                var archivedFolder = await ResolveArchivedFolderAsync(folder);
                var archivedItem = new FileItem
                {
                    Id = Guid.NewGuid(),
                    FolderId = archivedFolder.Id,
                    Name = $"{existing.Name} (v{oldVersion.VersionNumber})",
                    FileType = existing.FileType,
                    CurrentVersionId = oldVersion.Id,
                    CreatedByAccountId = actor,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _unitOfWork.Repository<FileItem>().CreateAsync(archivedItem);

                // Chuyển bản cũ sang file mục Archived (cập nhật FK).
                oldVersion.FileItemId = archivedItem.Id;
                oldVersion.IsHidden = false;
                archivedFileItemId = archivedItem.Id;
            }

            await StageRelatedFileLinksAsync(existing.Id, folder, dto, actor, isSystemAdmin);

            await _unitOfWork.CommitAsync();

            if (dto.FileType is FileType.Pdf or FileType.Office)
                _nameMatchContentBackgroundService.Enqueue(existing.Id);

            if (AutoTranslateModelsOnUpload && IsModelType(dto.FileType))
                _translationQueue.Enqueue(newVersion.Id);
            if (dto.FileType == FileType.Ifc)
                _loiCheckQueue.Enqueue(newVersion.Id);

            return new FileUploadResultDTO
            {
                FileItem = _mapper.Map<FileItemResponseDTO>(existing),
                Version = _mapper.Map<FileVersionResponseDTO>(newVersion),
                IsNewVersion = true,
                ArchivedFileItemId = archivedFileItemId,
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

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

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

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

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

        private static FileVersion NewVersion(
            Guid fileItemId, int number, StoredFile stored, string format, Guid actor, DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            FileItemId = fileItemId,
            VersionNumber = number,
            StoragePath = stored.RelativePath,
            FileSizeBytes = stored.SizeBytes,
            Format = format,
            Checksum = stored.Checksum,
            IsHidden = false,
            UploadedByAccountId = actor,
            UploadedAt = now
        };

        // Tìm folder Archived của nhóm sở hữu (đi ngược cây tìm OwnerGroupId), fallback về Archived gốc.
        private async Task<Folder> ResolveArchivedFolderAsync(Folder folder)
        {
            var projectFolders = (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ProjectId == folder.ProjectId))
                .ToList();
            var byId = projectFolders.ToDictionary(f => f.Id);

            // Xác định nhóm sở hữu: chính folder hoặc tổ tiên gần nhất có OwnerGroupId.
            var cur = folder;
           

            var archivedRoot = projectFolders.FirstOrDefault(
                f => f.ParentFolderId == null && f.Area == CdeArea.Archived)
                ?? throw new ApiExceptionResponse("Archived area is missing for this project.", 500);

            return archivedRoot;
        }

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
