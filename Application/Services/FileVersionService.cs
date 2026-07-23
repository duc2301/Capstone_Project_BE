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

        public async Task<FileVersionResult> RestoreVersionAsync(Guid fileItemId, Guid versionStateId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new KeyNotFoundException($"FileItem {fileItemId} not found.");

            var current = await RequireCurrentStateAsync(fileItemId);

            // Cùng luật "upload thay thế": tài liệu đang Published không nhận version mới, phải về WIP trước.
            if (current.Stage == VersionStage.Published)
                throw new InvalidOperationException(
                    "Published documents cannot restore a past version. Return the document to WIP first.");

            var source = await _unitOfWork.Repository<FileVersionState>().GetByIdAsync(versionStateId)
                ?? throw new KeyNotFoundException($"Version {versionStateId} not found.");

            if (source.FileItemId != fileItemId)
                throw new InvalidOperationException("Version does not belong to this file.");
            if (source.IsCurrent)
                throw new InvalidOperationException("This version is already the current version.");

            // Đánh số y hệt upload thay thế (WorkingVersion +1 trong cùng Working Revision),
            // nhưng dữ liệu file copy từ version ĐƯỢC CHỌN thay vì upload mới.
            var snapshot = BuildRestoreSnapshot(source,
                VersionStage.Working, current.WorkingRevision, current.WorkingVersion + 1, current.PublishedRevision);

            await PersistSnapshotAsync(snapshot, current);

            // Restore là feature thật (không phải sub-step của flow khác) nên tự cập nhật con trỏ
            // version hiện hành của FileItem để folder-contents/view... đọc đúng bản vừa khôi phục.
            fileItem.CurrentVersionId = snapshot.Id;
            fileItem.UpdatedAt = DateTime.UtcNow;
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
            CopyContentFrom(snapshot, current);
            return snapshot;
        }

        // Snapshot cho restore: copy dữ liệu file của version cũ ĐƯỢC CHỌN sang dòng mới.
        private static FileVersionState BuildRestoreSnapshot(
            FileVersionState source,
            VersionStage stage, int workingRevision, int workingVersion, int publishedRevision)
        {
            var snapshot = NewSnapshot(source.FileItemId, stage, workingRevision, workingVersion, publishedRevision);
            CopyContentFrom(snapshot, source);
            return snapshot;
        }

        // Chép toàn bộ dữ liệu file vật lý + viewer + chữ ký (không đụng số version) giữa 2 dòng state.
        private static void CopyContentFrom(FileVersionState target, FileVersionState source)
        {
            target.FileName = source.FileName;
            target.StoragePath = source.StoragePath;
            target.FileSizeBytes = source.FileSizeBytes;
            target.Format = source.Format;
            target.Checksum = source.Checksum;
            target.IsHidden = source.IsHidden;
            target.UploadedByAccountId = source.UploadedByAccountId;
            target.UploadedAt = source.UploadedAt;
            target.ViewerUrn = source.ViewerUrn;
            target.PreviewStoragePath = source.PreviewStoragePath;
            target.ViewerStatus = source.ViewerStatus;
            target.ViewerProgress = source.ViewerProgress;
            target.ViewerError = source.ViewerError;
            target.IsSigned = source.IsSigned;
            target.SignedAt = source.SignedAt;
            target.SignedBy = source.SignedBy;
            target.CertificateSerial = source.CertificateSerial;
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
