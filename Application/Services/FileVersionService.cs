using Application.DTOs.ResponseDTOs.FileVersion;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.File;

namespace Application.Services
{
    // Toàn bộ quy tắc versioning nằm ở đây — nơi khác chỉ gọi và dùng kết quả.
    // Quy tắc:
    //  - Working:   P{WorkingRevision:00}.{WorkingVersion:00}   (vd P01.02)
    //  - Published: C{PublishedRevision:00}                     (vd C01 — không có Version Number)
    //  - Upload thay thế       -> WorkingVersion +1
    //  - Vào SHARED thành công -> WorkingRevision +1, WorkingVersion = 1
    //  - Publish               -> PublishedRevision +1, Stage = Published
    //  - Về WIP từ Published   -> Stage = Working, giữ WorkingRevision, WorkingVersion = 1
    //
    // Lưu trữ: append-only — mỗi lần đổi version INSERT 1 dòng FileVersionState mới
    // (kèm snapshot dữ liệu file hiện hành), dòng cũ bị retire (IsCurrent = false), không update đè.
    public class FileVersionService : IFileVersionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FileVersionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<FileVersionResult> GetNextUploadVersionAsync(Guid folderId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required.", nameof(fileName));

            var existing = await _unitOfWork.FileVersionRepository.FindExistingDocumentAsync(folderId, fileName);

            // Chưa có FileItem trùng tên -> tài liệu mới hoàn toàn: trả P01.01 nhưng CHƯA lưu state
            // (chưa có FileItemId để gắn). Caller tạo FileItem xong gọi CreateInitialVersionAsync.
            if (existing == null)
                return ToResult(null, isNew: true, VersionStage.Working, workingRevision: 1, workingVersion: 1, publishedRevision: 0);

            var current = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(existing.Id);

            FileVersionState snapshot;
            if (current == null)
            {
                // Tài liệu cũ (tạo trước khi có File Versioning): seed từ số bản vật lý đã có.
                // Upload lần này là bản thay thế tiếp theo trong Revision 1.
                var existingVersionCount = await _unitOfWork.FileVersionRepository.CountFileVersionsAsync(existing.Id);
                snapshot = await BuildSnapshotAsync(existing.Id, existing.Name,
                    VersionStage.Working, current: null,
                    workingRevision: 1, workingVersion: existingVersionCount + 1, publishedRevision: 0);
            }
            else
            {
                if (current.Stage == VersionStage.Published)
                    throw new InvalidOperationException(
                        "Published documents cannot receive replacement uploads. Return the document to WIP first.");

                snapshot = await BuildSnapshotAsync(existing.Id, existing.Name,
                    VersionStage.Working, current,
                    current.WorkingRevision, current.WorkingVersion + 1, current.PublishedRevision);
            }

            await _unitOfWork.Repository<FileVersionState>().CreateAsync(snapshot);
            await _unitOfWork.SaveChangesAsync();
            return ToResult(snapshot);
        }

        public async Task<FileVersionResult> CreateInitialVersionAsync(Guid fileItemId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new KeyNotFoundException($"FileItem {fileItemId} not found.");

            var current = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(fileItemId);
            if (current != null)
                throw new InvalidOperationException(
                    $"FileItem {fileItemId} already has version state ({current.DisplayVersion}).");

            var snapshot = await BuildSnapshotAsync(fileItem.Id, fileItem.Name,
                VersionStage.Working, current: null,
                workingRevision: 1, workingVersion: 1, publishedRevision: 0);

            await _unitOfWork.Repository<FileVersionState>().CreateAsync(snapshot);
            await _unitOfWork.SaveChangesAsync();

            return ToResult(snapshot, isNew: true);
        }

        public async Task<FileVersionResult> GetNextSharedVersionAsync(Guid fileItemId)
        {
            var current = await RequireCurrentStateAsync(fileItemId);

            if (current.Stage == VersionStage.Published)
                throw new InvalidOperationException(
                    "Published documents cannot enter SHARED directly. Return the document to WIP first.");

            var snapshot = await BuildTransitionSnapshotAsync(fileItemId, current,
                VersionStage.Working, current.WorkingRevision + 1, workingVersion: 1, current.PublishedRevision);

            await _unitOfWork.Repository<FileVersionState>().CreateAsync(snapshot);
            await _unitOfWork.SaveChangesAsync();
            return ToResult(snapshot);
        }

        public async Task<FileVersionResult> GetNextPublishedVersionAsync(Guid fileItemId)
        {
            var current = await RequireCurrentStateAsync(fileItemId);

            if (current.Stage == VersionStage.Published)
                throw new InvalidOperationException("Document is already Published.");

            // WorkingRevision/WorkingVersion chép nguyên sang dòng mới — cần cho lần quay về WIP sau này.
            var snapshot = await BuildTransitionSnapshotAsync(fileItemId, current,
                VersionStage.Published, current.WorkingRevision, current.WorkingVersion, current.PublishedRevision + 1);

            await _unitOfWork.Repository<FileVersionState>().CreateAsync(snapshot);
            await _unitOfWork.SaveChangesAsync();
            return ToResult(snapshot);
        }

        public async Task<FileVersionResult> GetReturnToWipVersionAsync(Guid fileItemId)
        {
            var current = await RequireCurrentStateAsync(fileItemId);

            if (current.Stage != VersionStage.Published)
                throw new InvalidOperationException("Only Published documents can return to WIP.");

            // Giữ WorkingRevision, reset WorkingVersion, bảo toàn PublishedRevision.
            var snapshot = await BuildTransitionSnapshotAsync(fileItemId, current,
                VersionStage.Working, current.WorkingRevision, workingVersion: 1, current.PublishedRevision);

            await _unitOfWork.Repository<FileVersionState>().CreateAsync(snapshot);
            await _unitOfWork.SaveChangesAsync();
            return ToResult(snapshot);
        }

        public async Task<FileVersionResult?> GetCurrentVersionAsync(Guid fileItemId)
        {
            var current = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(fileItemId);
            return current == null ? null : ToResult(current);
        }

        public async Task<List<FileVersionHistoryItemDTO>> GetVersionHistoryAsync(Guid fileItemId)
        {
            var history = await _unitOfWork.FileVersionRepository.GetHistoryAsync(fileItemId);

            return history.Select(s => new FileVersionHistoryItemDTO
            {
                Id = s.Id,
                FileItemId = s.FileItemId,
                IsCurrent = s.IsCurrent,
                Stage = s.Stage,
                WorkingRevision = s.WorkingRevision,
                WorkingVersion = s.WorkingVersion,
                PublishedRevision = s.PublishedRevision,
                DisplayVersion = s.DisplayVersion,
                FileVersionId = s.FileVersionId,
                FileName = s.FileName,
                StoragePath = s.StoragePath,
                FileSizeBytes = s.FileSizeBytes,
                Format = s.Format,
                Checksum = s.Checksum,
                CreatedAt = s.CreatedAt
            }).ToList();
        }

        // --- Helpers ---

        private async Task<FileVersionState> RequireCurrentStateAsync(Guid fileItemId)
        {
            return await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(fileItemId)
                ?? throw new KeyNotFoundException(
                    $"FileItem {fileItemId} has no version state. Call CreateInitialVersionAsync first.");
        }

        // Snapshot cho zone transition (shared/publish/về WIP): tự lấy Name từ FileItem.
        private async Task<FileVersionState> BuildTransitionSnapshotAsync(
            Guid fileItemId, FileVersionState current,
            VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId);
            return await BuildSnapshotAsync(fileItemId, fileItem?.Name, stage, current,
                workingRevision, workingVersion, publishedRevision);
        }

        // Tạo dòng snapshot mới (IsCurrent = true) chép kèm dữ liệu file vật lý hiện hành,
        // đồng thời retire dòng hiện hành cũ (IsCurrent = false) nếu có.
        private async Task<FileVersionState> BuildSnapshotAsync(
            Guid fileItemId, string? fileName,
            VersionStage stage, FileVersionState? current,
            int workingRevision, int workingVersion, int publishedRevision)
        {
            if (current != null)
            {
                // Retire dòng cũ và LƯU TRƯỚC khi insert dòng mới — nếu để chung 1 SaveChanges,
                // EF có thể gửi INSERT trước UPDATE và vi phạm partial unique index
                // (mỗi FileItem chỉ được 1 dòng IsCurrent = true).
                current.IsCurrent = false;
                current.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }

            var currentFile = await _unitOfWork.FileVersionRepository.GetCurrentFileVersionAsync(fileItemId);
            var now = DateTime.UtcNow;

            return new FileVersionState
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItemId,
                IsCurrent = true,
                Stage = stage,
                WorkingRevision = workingRevision,
                WorkingVersion = workingVersion,
                PublishedRevision = publishedRevision,
                DisplayVersion = FormatDisplayVersion(stage, workingRevision, workingVersion, publishedRevision),
                FileVersionId = currentFile?.Id,
                FileName = fileName,
                StoragePath = currentFile?.StoragePath,
                FileSizeBytes = currentFile?.FileSizeBytes,
                Format = currentFile?.Format,
                Checksum = currentFile?.Checksum,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        private static string FormatDisplayVersion(VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            return stage == VersionStage.Published
                ? $"C{publishedRevision:00}"
                : $"P{workingRevision:00}.{workingVersion:00}";
        }

        private static FileVersionResult ToResult(FileVersionState state, bool isNew = false)
            => ToResult(state.FileItemId, isNew, state.Stage, state.WorkingRevision, state.WorkingVersion, state.PublishedRevision);

        private static FileVersionResult ToResult(
            Guid? fileItemId, bool isNew, VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            return new FileVersionResult
            {
                FileItemId = fileItemId,
                IsNewDocument = isNew,
                Stage = stage,
                WorkingRevision = workingRevision,
                WorkingVersion = workingVersion,
                PublishedRevision = publishedRevision,
                DisplayVersion = FormatDisplayVersion(stage, workingRevision, workingVersion, publishedRevision)
            };
        }
    }
}
