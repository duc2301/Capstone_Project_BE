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
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Domain.Enum.File;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Application.Services
{
    public class FileUploadService : IFileUploadService
    {
        private static readonly char[] IllegalNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        // Giới hạn độ dài tên tài liệu (xem ValidateName) — phần đặt lại tên "Tên (n)" phải tôn trọng.
        private const int MaxNameLength = 200;
        // Trần số lần thử "Tên (n)": chạm trần nghĩa là tên đó đang bị lạm dụng, bắt người dùng đặt tên tử tế.
        private const int MaxCopySuffix = 99;
        // Số hiệu ISO 19650 mặc định 4 chữ số (0001, 0002...); quét tối đa ngần này số kể từ số kế tiếp.
        private const int DefaultSequenceWidth = 4;
        private const int MaxSequenceScan = 999;

        // [CÔNG TẮC DEMO] Tự động biên dịch model IFC/CAD lên Autodesk APS NGAY khi upload (lưu sẵn ViewerUrn
        // để lúc mở "Xem chi tiết" không phải chờ dịch).
        //  - false (mặc định hiện tại): TẮT để khỏi ngốn dung lượng Autodesk (gói free) — nếu mọi model upload
        //    đều dịch & lưu trên APS thì rất nhanh hết quota. Model chỉ được dịch ON-DEMAND lúc người dùng lần đầu
        //    mở "Xem chi tiết" (xem FileViewService.BuildModelAsync: ViewerStatus = None -> tự đẩy vào hàng đợi).
        //  - true: BẬT lại khi DEMO với giáo viên để model dịch sẵn từ lúc upload, mở xem là có ngay (đỡ phải chờ).
        private static readonly bool AutoTranslateModelsOnUpload = false;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IPermissionCheckingService _permission;
        private readonly IFileStorageService _storage;
        private readonly ICdeStorageKeyBuilder _storageKey;
        private readonly IModelTranslationQueue _translationQueue;
        private readonly IMapper _mapper;
        private readonly INamingConventionService _naming;
        private readonly INameMatchContentBackgroundService _nameMatchContentBackgroundService;
        private readonly IFileVersionService _fileVersionService;
        private readonly IFileLinkService _fileLink;
        private readonly IAuditLogService _auditLog;
        private readonly IDocumentIndexSyncService _indexSync;
        private readonly ILogger<FileUploadService> _logger;

        public FileUploadService(IUnitOfWork unitOfWork, IFileStorageService storage, ICdeStorageKeyBuilder storageKey, IModelTranslationQueue translationQueue, IMapper mapper, INamingConventionService naming, INameMatchContentBackgroundService nameMatchContentBackgroundService, IFileVersionService fileVersionService, IFileLinkService fileLink, IAuditLogService auditLog, IPermissionCheckingService permission, IDocumentIndexSyncService indexSync, ILogger<FileUploadService> logger)
        {
            _logger = logger;
            _indexSync = indexSync;
            _auditLog = auditLog;
            _permission = permission;
            _unitOfWork = unitOfWork;
            _storage = storage;
            _storageKey = storageKey;
            _translationQueue = translationQueue;
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

            if (folder.ParentFolderId == null)
                throw new ApiExceptionResponse(
                    "Không thể tải file trực tiếp lên thư mục gốc. Tạo thư mục con để upload thay thế.", 400);

            if (folder.Area != CdeArea.Wip && !await IsSystemDocumentFolderAsync(folder))
                throw new ApiExceptionResponse(
                    "Chỉ được tải file lên khu vực WIP. File sang Shared/Published qua luồng phê duyệt.", 400);

            if (!isSystemAdmin)
                await _permission.CanUploadToFolderAsync(folder.Id, actor);

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

            var format = ext.TrimStart('.').ToLowerInvariant();

            // ② Tệp liên quan: KIỂM phạm vi TRƯỚC khi lưu file — id sai/ngoài phạm vi thì fail ở đây,
            // chưa lưu byte nào (hệ versioning mới commit FileItem giữa luồng, không thể rollback file mồ côi).
            if (dto.RelatedFileItemIds is { Count: > 0 })
                await _fileLink.ValidateUploadLinkTargetsAsync(folder, dto.RelatedFileItemIds, actor, ct);

            // ⑤bis "Lên phiên bản" = SỬA chính tài liệu đang có -> phải có quyền Sửa trên FILE đó,
            // không chỉ quyền upload vào THƯ MỤC. Quyền upload thư mục (CanUploadToFolderAsync ở trên)
            // không xét override/khoá cấp file, nên nếu bỏ qua bước này, người bị chặn/chỉ-đọc trên
            // đúng tài liệu vẫn đè được phiên bản mới lên. Chốt TRƯỚC khi lưu bytes để không sinh object mồ côi.
            // (Nhánh "tài liệu riêng" tạo FileItem MỚI -> quyền thư mục là đủ, không xét ở đây.)
            if (!isSystemAdmin && dto.DuplicateAction == UploadDuplicateAction.NewVersion)
            {
                var conflict = await _fileVersionService.CheckNameAvailabilityAsync(folder.Id, name, format);
                if (conflict.Scope == NameConflictScope.SameFolder && conflict.ConflictFileItemId.HasValue)
                    await _permission.CanEditFileAsync(conflict.ConflictFileItemId.Value, actor);
            }

            // ⑥ Trùng tên trong thư mục đích: hệ thống KHÔNG tự quyết nữa. Người dùng phải chọn lên
            // phiên bản của tài liệu đang có hay tách thành tài liệu riêng (đổi tên). Chốt TRƯỚC khi
            // lưu bytes vì lựa chọn "tài liệu riêng" đổi luôn tên -> đổi cả tên object trên kho.
            name = await ResolveDuplicateNameAsync(
                folder.Id, name, format, dto.DuplicateAction, naming.HasNamingConvention);

            // ⑦ Lưu nội dung file. Nhãn version lấy trước chỉ để ĐẶT TÊN object cho dễ đọc trên kho —
            // số version thật do FileVersionService chốt ở bước ⑤ bên dưới (DB là nguồn sự thật).
            // Phải peek vì bước ⑤ cần StoragePath nên không thể chạy trước bước lưu file này.
            var versionLabel = await _fileVersionService.PeekNextUploadVersionAsync(folder.Id, name);
            var objectName = await _storageKey.ForDocumentAsync(folder.Id, name, versionLabel, ext, ct);
            var stored = await _storage.SaveAsync(content, objectName, ct);
            var url = await _storage.GetPresignedUrlAsync(stored.RelativePath, 60, ct);
            var now = DateTime.UtcNow;

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
                // vd: tài liệu đang Published, hoặc trùng tên trong phạm vi dự án.
                // Bytes đã nằm trên kho từ bước ⑦ nhưng chưa dòng DB nào trỏ tới -> dọn ngay,
                // không thì mỗi lần người dùng bấm nhầm là bucket thừa một object mồ côi.
                // An toàn: cả hai nhánh ném lỗi đều xảy ra TRƯỚC khi versioning ghi bất cứ thứ gì,
                // nên không có dòng state nào đang trỏ vào object này.
                await TryDeleteOrphanAsync(stored.RelativePath, ct);
                throw new ApiExceptionResponse(ex.Message, 409);
            }

            // Giữ cờ "tài liệu mới" TRƯỚC khi `version` bị gán lại trong nhánh dưới (dùng cho audit log).
            var isNewDocument = version.IsNewDocument;

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
            await StageRelatedFileLinksAsync(fileItem.Id, folder, dto, actor);

            await _auditLog.LogAsync(
                LogScope.Group,
                isNewDocument ? AuditAction.Upload : AuditAction.NewVersion,
                nameof(FileItem), fileItem.Id.ToString(), actor,
                detail: isNewDocument
                    ? $"Tải lên tài liệu mới '{fileItem.Name}' (v{version.DisplayVersion})"
                    : $"Cập nhật phiên bản '{fileItem.Name}' (v{version.DisplayVersion})",
                projectId: folder.ProjectId, folderId: folder.Id);

            await _unitOfWork.CommitAsync();

            if (AutoTranslateModelsOnUpload && IsModelType(dto.FileType))
                _translationQueue.Enqueue(version.VersionStateId!.Value);

            _nameMatchContentBackgroundService.Enqueue(fileItem.Id);

            // Upload thẳng vào thư mục hệ thống ở Published (Hồ sơ pháp lý / tài liệu gói thầu) không đi qua
            // luồng phê duyệt -> đây là đường thứ ba tệp tới vùng chính thức, phải tự xin index.
            // Upload vào WIP thì RequestIndexAsync tự bỏ qua.
            await _indexSync.RequestIndexAsync(fileItem.Id, ct);

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

        // Hỏi trước khi tải bytes: tên này còn trống không, bận thì bận ở đâu, còn lựa chọn nào.
        // Có endpoint riêng vì file CDE hay nặng hàng trăm MB — tải xong mới báo trùng là quá muộn.
        public async Task<NameAvailabilityDTO> CheckNameAvailabilityAsync(
            Guid folderId, string name, string format, bool bypassNamingConvention,
            Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            // Tên tài liệu trong dự án cũng là thông tin — chỉ người được phép upload vào đây mới hỏi được.
            if (!isSystemAdmin)
                await _permission.CanUploadToFolderAsync(folder.Id, actor);

            var trimmedName = (name ?? string.Empty).Trim();
            var trimmedFormat = (format ?? string.Empty).TrimStart('.').ToLowerInvariant();

            var availability = await _fileVersionService
                .CheckNameAvailabilityAsync(folder.Id, trimmedName, trimmedFormat);
            if (availability.IsAvailable)
                return availability;

            // Tách thành tài liệu riêng luôn khả thi — chỉ khác cách đặt tên: thư mục áp quy tắc thì
            // cấp SỐ HIỆU tiếp theo (ISO 19650), tên tự do thì thêm hậu tố "(n)".
            var folderNaming = bypassNamingConvention ? null : await _naming.GetByFolderAsync(folder.Id);

            availability.CanCreateNewDocument = true;
            availability.SuggestedName = await ResolveNewDocumentNameAsync(
                folder.Id, trimmedName, trimmedFormat, folderNaming);

            return availability;
        }

        // Tên cho tài liệu riêng, theo đúng "ngôn ngữ" đặt tên của thư mục đích.
        private async Task<string> ResolveNewDocumentNameAsync(
            Guid folderId, string name, string format, FolderNamingConventionResponseDTO? folderNaming)
        {
            return folderNaming?.HasNamingConvention == true
                ? await ResolveNextSequenceNameAsync(folderId, name, format, folderNaming.Delimiter)
                : await ResolveAvailableNameAsync(folderId, name, format);
        }

        // Tên sẽ dùng thật khi trùng: giữ nguyên (lên phiên bản của tài liệu đang có) hoặc một tên
        // còn trống (tách tài liệu riêng). Không khai báo ý định thì từ chối, không đoán hộ.
        private async Task<string> ResolveDuplicateNameAsync(
            Guid folderId, string name, string format, UploadDuplicateAction action, bool namingEnforced)
        {
            var availability = await _fileVersionService.CheckNameAvailabilityAsync(folderId, name, format);
            if (availability.Scope == NameConflictScope.None)
                return name;

            switch (action)
            {
                // "Lên phiên bản" chỉ có nghĩa khi tài liệu nằm ngay thư mục đích. Trùng ở khu vực
                // khác thì trả tên nguyên vẹn để GetNextUploadVersionAsync chặn kèm hướng dẫn của nó
                // (một thông điệp, một chỗ viết).
                case UploadDuplicateAction.NewVersion:
                    return name;

                // Đổi tên áp dụng cho cả hai phạm vi trùng: tên mới phải trống ở cả thư mục lẫn dự án.
                // Thư mục áp quy tắc đặt tên -> cấp số hiệu tiếp theo (ISO 19650), không bịa hậu tố.
                case UploadDuplicateAction.NewDocument:
                    return await ResolveNewDocumentNameAsync(
                        folderId, name, format,
                        namingEnforced ? await _naming.GetByFolderAsync(folderId) : null);

                // Chưa hỏi người dùng mà trùng ngoài thư mục: GetNextUploadVersionAsync có sẵn thông
                // điệp chi tiết theo khu vực -> để nó nói, đừng viết lại một phiên bản nghèo hơn.
                case UploadDuplicateAction.None when availability.Scope == NameConflictScope.OtherFolder:
                    return name;

                default:
                    var revision = string.IsNullOrEmpty(availability.ConflictDisplayVersion)
                        ? string.Empty
                        : $" (phiên bản {availability.ConflictDisplayVersion})";
                    throw new ApiExceptionResponse(
                        $"Thư mục đã có tài liệu '{name}'{revision}. Chọn tạo phiên bản mới cho tài "
                        + "liệu này, hoặc lưu thành tài liệu riêng, rồi tải lên lại.", 409);
            }
        }

        // Tên trống gần nhất theo kiểu "Tên (2)", "Tên (3)"... Mỗi ứng viên đều hỏi lại đúng luật
        // trùng tên nên tên trả về chắc chắn tạo được tài liệu mới, không phải đoán.
        private async Task<string> ResolveAvailableNameAsync(Guid folderId, string name, string format)
        {
            var baseName = StripCopySuffix(name);

            for (var i = 2; i <= MaxCopySuffix; i++)
            {
                var suffix = $" ({i})";
                // Tên có đuôi "(n)" vẫn phải lọt giới hạn độ dài của ValidateName -> cắt bớt phần đầu.
                var head = baseName.Length + suffix.Length > MaxNameLength
                    ? baseName[..(MaxNameLength - suffix.Length)].TrimEnd()
                    : baseName;

                var candidate = head + suffix;
                if ((await _fileVersionService.CheckNameAvailabilityAsync(folderId, candidate, format)).IsAvailable)
                    return candidate;
            }

            throw new ApiExceptionResponse(
                $"Đã có quá nhiều tài liệu tên '{baseName}' trong dự án. Đặt tên khác để phân biệt.", 409);
        }

        /* ISO 19650: mã tài liệu (container identifier) phải DUY NHẤT trong dự án, và hai tài liệu
         * khác nhau cùng bộ giá trị (dự án - đơn vị - phân khu - cao độ - loại - vai trò) được phân
         * biệt bằng SỐ HIỆU tuần tự ở cuối mã. Vậy nên "thêm tài liệu riêng" trong thư mục áp quy
         * tắc đặt tên = cấp số hiệu tiếp theo, KHÔNG bịa hậu tố kiểu "(2)" (làm hỏng mã chuẩn).
         *  - Mã đã có số hiệu ("...-0007") -> đếm tiếp từ đó, giữ nguyên số chữ số.
         *  - Mã chưa có -> mở số hiệu đầu tiên, 4 chữ số theo thông lệ ISO 19650. */
        private async Task<string> ResolveNextSequenceNameAsync(
            Guid folderId, string name, string format, string? delimiter)
        {
            var separator = string.IsNullOrEmpty(delimiter) ? "-" : delimiter;
            var (stem, next, width) = SplitTrailingSequence(name.Trim(), separator);

            for (var i = next; i < next + MaxSequenceScan; i++)
            {
                var candidate = $"{stem}{separator}{i.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')}";
                if (candidate.Length > MaxNameLength)
                    break;

                if ((await _fileVersionService.CheckNameAvailabilityAsync(folderId, candidate, format)).IsAvailable)
                    return candidate;
            }

            throw new ApiExceptionResponse(
                $"Không cấp được số hiệu mới cho '{stem}'. Đổi giá trị trong quy tắc đặt tên để sinh mã khác.", 409);
        }

        // "DC.V2J.ZZ.CO.W-0007" -> ("DC.V2J.ZZ.CO.W", 8, 4); "DC.V2J.ZZ.CO.W" -> (chính nó, 1, 4).
        private static (string Stem, int Next, int Width) SplitTrailingSequence(string name, string separator)
        {
            var idx = name.LastIndexOf(separator, StringComparison.Ordinal);
            if (idx > 0 && idx + separator.Length < name.Length)
            {
                var tail = name[(idx + separator.Length)..];
                if (tail.All(char.IsDigit) && int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var current))
                    return (name[..idx], current + 1, tail.Length);
            }

            return (name, 1, DefaultSequenceWidth);
        }

        // "Plan (2)" -> "Plan": tách lần nữa phải ra "Plan (3)", không phải "Plan (2) (2)".
        private static string StripCopySuffix(string name)
        {
            var stripped = Regex.Replace(name.Trim(), @"\s*\(\d+\)$", string.Empty).Trim();
            return string.IsNullOrWhiteSpace(stripped) ? name.Trim() : stripped;
        }

        // Dọn object vừa ghi khi luồng upload bị từ chối sau đó. Không để lỗi xoá nổi lên trên:
        // lỗi nghiệp vụ gốc mới là thứ người dùng cần thấy, còn object sót lại thì ghi log để dọn sau.
        private async Task TryDeleteOrphanAsync(string storagePath, CancellationToken ct)
        {
            try
            {
                await _storage.DeleteAsync(storagePath, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Không xoá được object mồ côi {StoragePath} sau khi upload bị từ chối.", storagePath);
            }
        }

        public async Task<DownloadFileResult> OpenDownloadAsync(Guid fileItemId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.CanViewFileAsync(fileItem.Id, actor);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            return await OpenVersionContentAsync(fileItem, version, actor, ct);
        }

        public async Task<DownloadFileResult> OpenVersionDownloadAsync(
            Guid fileItemId, Guid versionStateId, Guid actor, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.CanViewFileAsync(fileItem.Id, actor);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(versionStateId)
                ?? throw new ApiExceptionResponse("Version not found.", 404);

            if (version.FileItemId != fileItem.Id)
                throw new ApiExceptionResponse("Version does not belong to this file.", 400);

            return await OpenVersionContentAsync(fileItem, version, actor, ct);
        }

        private async Task<DownloadFileResult> OpenVersionContentAsync(
            FileItem fileItem, FileVersionState version, Guid actor, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(version.StoragePath) || string.IsNullOrEmpty(version.Format))
                throw new ApiExceptionResponse("Version has no stored content.", 404);

            var stream = await _storage.OpenReadAsync(version.StoragePath, ct);
            var downloadName = FileDownloadNaming.BuildFileName(fileItem.Name, version.Format);

            // Luồng chỉ-đọc: không có transaction nghiệp vụ để bám vào -> ghi + commit riêng.
            var downloadFolder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);
            await _auditLog.LogAndSaveAsync(
                LogScope.Group, AuditAction.Download, nameof(FileItem), fileItem.Id.ToString(), actor,
                detail: $"Tải về '{downloadName}' ({version.DisplayVersion})",
                projectId: downloadFolder?.ProjectId, folderId: fileItem.FolderId);

            return new DownloadFileResult(stream, downloadName, _storage.GetContentType(version.Format));
        }

        public async Task<string?> GetViewUrlAsync(Guid fileItemId, Guid actor, int minutes = 60, CancellationToken ct = default)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.CanViewFileAsync(fileItem.Id, actor);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            if (string.IsNullOrEmpty(version.StoragePath))
                throw new ApiExceptionResponse("Current version has no stored content.", 404);

            // Cấp link xem trực tiếp cũng là một lượt truy cập nội dung -> phải vào nhật ký,
            // giống Download ở OpenVersionContentAsync. Gộp trùng để mở lại không đẻ log rác.
            var urlFolder = await _unitOfWork.Repository<Folder>().GetByIdAsync(fileItem.FolderId);
            await _auditLog.LogThrottledAsync(
                LogScope.Group, AuditAction.View, nameof(FileItem), fileItem.Id.ToString(), actor,
                detail: $"Xem '{fileItem.Name}' ({version.DisplayVersion})",
                projectId: urlFolder?.ProjectId, folderId: fileItem.FolderId);

            return await _storage.GetPresignedUrlAsync(version.StoragePath, minutes, ct);
        }

        // ---------- nội bộ ----------

        private async Task StageRelatedFileLinksAsync(
            Guid fileItemId, Folder targetFolder, UploadFileDTO dto, Guid actor)
        {
            if (dto.RelatedFileItemIds is not { Count: > 0 }) return;

            await _fileLink.StageLinksOnUploadAsync(
                fileItemId, targetFolder, dto.RelatedFileItemIds, actor);
        }

        // Chỉ model IFC/CAD mới cần dịch lên APS (xem ModelTranslationWorker).
        private static bool IsModelType(FileType type) => type is FileType.Ifc or FileType.Cad;

        private async Task<bool> IsSystemDocumentFolderAsync(Folder folder)
        {
            if (folder.Area != CdeArea.Published) return false;

            if (folder.Name == FolderBootstrapService.LegalDocumentsFolderName) return true;

            return (await _unitOfWork.Repository<ContractPackage>()
                .FindAsync(p => p.DocumentFolderId == folder.Id)).Any();
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
