using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Application.Services
{
    public class FileUploadService : IFileUploadService
    {
        private static readonly char[] IllegalNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IFolderPermissionService _permission;
        private readonly IFileStorageService _storage;
        private readonly IMapper _mapper;

        public FileUploadService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IFolderPermissionService permission,
            IFileStorageService storage,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _permission = permission;
            _storage = storage;
            _mapper = mapper;
        }

        public async Task<FileUploadResultDTO> UploadAsync(
            UploadFileDTO dto, Stream content, string originalFileName, CancellationToken ct = default)
        {
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(dto.FolderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);

            // ④ Consistency: chỉ được upload vào WIP/Shared (Published/Archived là khu xuất bản/lưu trữ).
            if (folder.Area is CdeArea.Published or CdeArea.Archived)
                throw new ApiExceptionResponse(
                    "Cannot upload directly into Published/Archived. Use WIP or Shared.", 400);
            if (folder.ParentFolderId == null)
                throw new ApiExceptionResponse(
                    "Cannot upload into a root area folder. Upload into a group/sub-folder.", 400);

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
            var siblings = (await _unitOfWork.Repository<FileItem>().GetAllAsync())
                .Where(f => f.FolderId == folder.Id)
                .ToList();
            var existing = siblings.FirstOrDefault(
                f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            var isNewVersion = existing != null;

            // ① Đối chiếu quyền: phiên bản mới cần Update, file mới cần Edit.
            await _permission.RequireAsync(actor, folder.Id, isNewVersion ? FolderAction.Update : FolderAction.Edit);

            // ⑦ Lưu nội dung file (đĩa local).
            var stored = await _storage.SaveAsync(content, folder.ProjectId, folder.Id, ext, ct);
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

                await _unitOfWork.Repository<FileItem>().CreateAsync(fileItem);
                await _unitOfWork.Repository<FileVersion>().CreateAsync(v1);
                await _unitOfWork.CommitAsync();

                return new FileUploadResultDTO
                {
                    FileItem = _mapper.Map<FileItemResponseDTO>(fileItem),
                    Version = _mapper.Map<FileVersionResponseDTO>(v1),
                    IsNewVersion = false
                };
            }

            // --- Phiên bản mới của file đã tồn tại ---
            var versions = (await _unitOfWork.Repository<FileVersion>().GetAllAsync())
                .Where(v => v.FileItemId == existing!.Id)
                .ToList();
            var nextNo = (versions.Count == 0 ? 0 : versions.Max(v => v.VersionNumber)) + 1;

            var oldVersion = versions.FirstOrDefault(v => v.Id == existing!.CurrentVersionId)
                             ?? versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

            var newVersion = NewVersion(existing!.Id, nextNo, stored, format, actor, now);
            await _unitOfWork.Repository<FileVersion>().CreateAsync(newVersion);

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

            await _unitOfWork.CommitAsync();

            return new FileUploadResultDTO
            {
                FileItem = _mapper.Map<FileItemResponseDTO>(existing),
                Version = _mapper.Map<FileVersionResponseDTO>(newVersion),
                IsNewVersion = true,
                ArchivedFileItemId = archivedFileItemId
            };
        }

        public async Task<DownloadFileResult> OpenDownloadAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var actor = _currentUser.AccountId
                ?? throw new ApiExceptionResponse("Authentication required.", 401);

            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            await _permission.RequireAsync(actor, fileItem.FolderId, FolderAction.Download);

            if (!fileItem.CurrentVersionId.HasValue)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _unitOfWork.Repository<FileVersion>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);

            var stream = await _storage.OpenReadAsync(version.StoragePath, ct);
            var downloadName = $"{fileItem.Name}.{version.Format}";
            return new DownloadFileResult(stream, downloadName, _storage.GetContentType(version.Format));
        }

        // ---------- nội bộ ----------

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
            var projectFolders = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == folder.ProjectId)
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
                throw new ApiExceptionResponse("File name is required.", 400);
            if (name.IndexOfAny(IllegalNameChars) >= 0)
                throw new ApiExceptionResponse("File name contains invalid characters ( \\ / : * ? \" < > | ).", 400);
            if (name.Length > 200)
                throw new ApiExceptionResponse("File name is too long (max 200).", 400);
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
