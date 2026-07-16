using Application.DTOs.RequestDTOs.FileVersion;
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
    //  - Upload thay thế       -> WorkingVersion +1 (dữ liệu file mới do caller truyền vào)
    //  - Vào SHARED thành công -> WorkingRevision +1, WorkingVersion = 1 (dữ liệu file copy từ dòng trước)
    //  - Publish               -> PublishedRevision +1, Stage = Published (copy dữ liệu file)
    //  - Về WIP từ Published   -> Stage = Working, giữ WorkingRevision, WorkingVersion = 1 (copy dữ liệu file)
    //
    // Lưu trữ: append-only — mỗi lần đổi version INSERT 1 dòng FileVersionState mới,
    // dòng cũ bị retire (IsCurrent = false), không update đè. Không đọc/ghi bảng FileVersions cũ.
    public class FileVersionService : IFileVersionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FileVersionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<FileVersionResult> GetNextUploadVersionAsync(Guid folderId, string fileName, FileVersionDataDTO? fileData = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name is required.", nameof(fileName));

            var existing = await _unitOfWork.FileVersionRepository.FindExistingDocumentAsync(folderId, fileName);

            // Chưa có FileItem trùng tên -> tài liệu mới hoàn toàn: trả P01.01 nhưng CHƯA lưu state
            // (chưa có FileItemId để gắn). Caller tạo FileItem xong gọi CreateInitialVersionAsync.
            if (existing == null)
                return ToResult(null, null, isNew: true, VersionStage.Working, workingRevision: 1, workingVersion: 1, publishedRevision: 0);

            var current = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(existing.Id);

            FileVersionState snapshot;
            if (current == null)
            {
                // FileItem có sẵn nhưng chưa có state (không còn hệ versioning cũ nào seed nữa):
                // coi upload này là version đầu tiên của tài liệu.
                snapshot = BuildUploadSnapshot(existing.Id, existing.Name, fileData, current: null,
                    VersionStage.Working, workingRevision: 1, workingVersion: 1, publishedRevision: 0);
            }
            else
            {
                if (current.Stage == VersionStage.Published)
                    throw new InvalidOperationException(
                        "Published documents cannot receive replacement uploads. Return the document to WIP first.");

                snapshot = BuildUploadSnapshot(existing.Id, existing.Name, fileData, current,
                    VersionStage.Working, current.WorkingRevision, current.WorkingVersion + 1, current.PublishedRevision);
            }

            await PersistSnapshotAsync(snapshot, current);
            return ToResult(snapshot);
        }

        public async Task<FileVersionResult> CreateInitialVersionAsync(Guid fileItemId, FileVersionDataDTO? fileData = null)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new KeyNotFoundException($"FileItem {fileItemId} not found.");

            var current = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(fileItemId);
            if (current != null)
                throw new InvalidOperationException(
                    $"FileItem {fileItemId} already has version state ({current.DisplayVersion}).");

            var snapshot = BuildUploadSnapshot(fileItem.Id, fileItem.Name, fileData, current: null,
                VersionStage.Working, workingRevision: 1, workingVersion: 1, publishedRevision: 0);

            await PersistSnapshotAsync(snapshot, current: null);
            return ToResult(snapshot, isNew: true);
        }

        public async Task<FileVersionResult> GetNextSharedVersionAsync(Guid fileItemId)
        {
            var current = await RequireCurrentStateAsync(fileItemId);

            if (current.Stage == VersionStage.Published)
                throw new InvalidOperationException(
                    "Published documents cannot enter SHARED directly. Return the document to WIP first.");

            var snapshot = BuildTransitionSnapshot(current,
                VersionStage.Working, current.WorkingRevision + 1, workingVersion: 1, current.PublishedRevision);

            await PersistSnapshotAsync(snapshot, current);
            return ToResult(snapshot);
        }

        public async Task<FileVersionResult> GetNextPublishedVersionAsync(Guid fileItemId)
        {
            var current = await RequireCurrentStateAsync(fileItemId);

            if (current.Stage == VersionStage.Published)
                throw new InvalidOperationException("Document is already Published.");

            // WorkingRevision/WorkingVersion chép nguyên sang dòng mới — cần cho lần quay về WIP sau này.
            var snapshot = BuildTransitionSnapshot(current,
                VersionStage.Published, current.WorkingRevision, current.WorkingVersion, current.PublishedRevision + 1);

            await PersistSnapshotAsync(snapshot, current);
            return ToResult(snapshot);
        }

        public async Task<FileVersionResult> GetReturnToWipVersionAsync(Guid fileItemId)
        {
            var current = await RequireCurrentStateAsync(fileItemId);

            if (current.Stage != VersionStage.Published)
                throw new InvalidOperationException("Only Published documents can return to WIP.");

            // Giữ WorkingRevision, reset WorkingVersion, bảo toàn PublishedRevision.
            var snapshot = BuildTransitionSnapshot(current,
                VersionStage.Working, current.WorkingRevision, workingVersion: 1, current.PublishedRevision);

            await PersistSnapshotAsync(snapshot, current);
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

        // Snapshot cho upload: dữ liệu file vật lý MỚI do caller truyền vào.
        private static FileVersionState BuildUploadSnapshot(
            Guid fileItemId, string? fileName, FileVersionDataDTO? fileData, FileVersionState? current,
            VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            var snapshot = NewSnapshot(fileItemId, stage, workingRevision, workingVersion, publishedRevision);
            snapshot.FileName = fileName;
            snapshot.StoragePath = fileData?.StoragePath;
            snapshot.FileSizeBytes = fileData?.FileSizeBytes;
            snapshot.Format = fileData?.Format;
            snapshot.Checksum = fileData?.Checksum;
            snapshot.UploadedByAccountId = fileData?.UploadedByAccountId;
            snapshot.UploadedAt = snapshot.CreatedAt;
            snapshot.ViewerStatus = fileData?.ViewerStatus ?? ModelViewerStatus.None;
            snapshot.IsSigned = fileData?.IsSigned ?? false;
            snapshot.SignedAt = fileData?.SignedAt;
            snapshot.SignedBy = fileData?.SignedBy;
            snapshot.CertificateSerial = fileData?.CertificateSerial;
            return snapshot;
        }

        // Snapshot cho zone transition (shared/publish/về WIP): file vật lý không đổi —
        // copy toàn bộ dữ liệu file + viewer + chữ ký từ dòng state đang retire.
        private static FileVersionState BuildTransitionSnapshot(
            FileVersionState current,
            VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            var snapshot = NewSnapshot(current.FileItemId, stage, workingRevision, workingVersion, publishedRevision);
            snapshot.FileVersionId = current.FileVersionId;
            snapshot.FileName = current.FileName;
            snapshot.StoragePath = current.StoragePath;
            snapshot.FileSizeBytes = current.FileSizeBytes;
            snapshot.Format = current.Format;
            snapshot.Checksum = current.Checksum;
            snapshot.IsHidden = current.IsHidden;
            snapshot.UploadedByAccountId = current.UploadedByAccountId;
            snapshot.UploadedAt = current.UploadedAt;
            snapshot.ViewerUrn = current.ViewerUrn;
            snapshot.PreviewStoragePath = current.PreviewStoragePath;
            snapshot.ViewerStatus = current.ViewerStatus;
            snapshot.ViewerProgress = current.ViewerProgress;
            snapshot.ViewerError = current.ViewerError;
            snapshot.IsSigned = current.IsSigned;
            snapshot.SignedAt = current.SignedAt;
            snapshot.SignedBy = current.SignedBy;
            snapshot.CertificateSerial = current.CertificateSerial;
            return snapshot;
        }

        private static FileVersionState NewSnapshot(
            Guid fileItemId, VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
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
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        // Retire dòng cũ (nếu có) rồi insert dòng mới — 2 SaveChanges tách biệt vì nếu để chung
        // 1 batch, EF có thể gửi INSERT trước UPDATE và vi phạm partial unique index
        // (mỗi FileItem chỉ được 1 dòng IsCurrent = true).
        private async Task PersistSnapshotAsync(FileVersionState snapshot, FileVersionState? current)
        {
            if (current != null)
            {
                current.IsCurrent = false;
                current.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }

            await _unitOfWork.Repository<FileVersionState>().CreateAsync(snapshot);
            await _unitOfWork.SaveChangesAsync();
        }

        private static string FormatDisplayVersion(VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            return stage == VersionStage.Published
                ? $"C{publishedRevision:00}"
                : $"P{workingRevision:00}.{workingVersion:00}";
        }

        private static FileVersionResult ToResult(FileVersionState state, bool isNew = false)
            => ToResult(state.FileItemId, state.Id, isNew, state.Stage, state.WorkingRevision, state.WorkingVersion, state.PublishedRevision);

        private static FileVersionResult ToResult(
            Guid? fileItemId, Guid? versionStateId, bool isNew, VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            return new FileVersionResult
            {
                FileItemId = fileItemId,
                VersionStateId = versionStateId,
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
