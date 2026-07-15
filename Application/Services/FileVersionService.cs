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

            var state = await _unitOfWork.FileVersionRepository.GetVersionStateAsync(existing.Id);

            if (state == null)
            {
                // Tài liệu cũ (tạo trước khi có File Versioning): seed từ số bản vật lý đã có.
                // Upload lần này là bản thay thế tiếp theo trong Revision 1.
                var existingVersionCount = await _unitOfWork.FileVersionRepository.CountFileVersionsAsync(existing.Id);
                state = NewState(existing.Id, workingRevision: 1, workingVersion: existingVersionCount + 1);
                await _unitOfWork.Repository<FileVersionState>().CreateAsync(state);
            }
            else
            {
                if (state.Stage == VersionStage.Published)
                    throw new InvalidOperationException(
                        "Published documents cannot receive replacement uploads. Return the document to WIP first.");

                state.WorkingVersion += 1;
                Touch(state);
            }

            await _unitOfWork.SaveChangesAsync();
            return ToResult(state);
        }

        public async Task<FileVersionResult> CreateInitialVersionAsync(Guid fileItemId)
        {
            var fileItem = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new KeyNotFoundException($"FileItem {fileItemId} not found.");

            var state = await _unitOfWork.FileVersionRepository.GetVersionStateAsync(fileItemId);
            if (state != null)
                throw new InvalidOperationException(
                    $"FileItem {fileItemId} already has version state ({state.DisplayVersion}).");

            state = NewState(fileItem.Id, workingRevision: 1, workingVersion: 1);
            await _unitOfWork.Repository<FileVersionState>().CreateAsync(state);
            await _unitOfWork.SaveChangesAsync();

            return ToResult(state, isNew: true);
        }

        public async Task<FileVersionResult> GetNextSharedVersionAsync(Guid fileItemId)
        {
            var state = await RequireStateAsync(fileItemId);

            if (state.Stage == VersionStage.Published)
                throw new InvalidOperationException(
                    "Published documents cannot enter SHARED directly. Return the document to WIP first.");

            state.WorkingRevision += 1;
            state.WorkingVersion = 1;
            Touch(state);

            await _unitOfWork.SaveChangesAsync();
            return ToResult(state);
        }

        public async Task<FileVersionResult> GetNextPublishedVersionAsync(Guid fileItemId)
        {
            var state = await RequireStateAsync(fileItemId);

            if (state.Stage == VersionStage.Published)
                throw new InvalidOperationException("Document is already Published.");

            // WorkingRevision/WorkingVersion giữ nguyên nội bộ — cần cho lần quay về WIP sau này.
            state.Stage = VersionStage.Published;
            state.PublishedRevision += 1;
            Touch(state);

            await _unitOfWork.SaveChangesAsync();
            return ToResult(state);
        }

        public async Task<FileVersionResult> GetReturnToWipVersionAsync(Guid fileItemId)
        {
            var state = await RequireStateAsync(fileItemId);

            if (state.Stage != VersionStage.Published)
                throw new InvalidOperationException("Only Published documents can return to WIP.");

            // Giữ WorkingRevision, reset WorkingVersion, bảo toàn PublishedRevision.
            state.Stage = VersionStage.Working;
            state.WorkingVersion = 1;
            Touch(state);

            await _unitOfWork.SaveChangesAsync();
            return ToResult(state);
        }

        public async Task<FileVersionResult?> GetCurrentVersionAsync(Guid fileItemId)
        {
            var state = await _unitOfWork.FileVersionRepository.GetVersionStateAsync(fileItemId);
            return state == null ? null : ToResult(state);
        }

        // --- Helpers ---

        private async Task<FileVersionState> RequireStateAsync(Guid fileItemId)
        {
            return await _unitOfWork.FileVersionRepository.GetVersionStateAsync(fileItemId)
                ?? throw new KeyNotFoundException(
                    $"FileItem {fileItemId} has no version state. Call CreateInitialVersionAsync first.");
        }

        private static FileVersionState NewState(Guid fileItemId, int workingRevision, int workingVersion)
        {
            var state = new FileVersionState
            {
                Id = Guid.NewGuid(),
                FileItemId = fileItemId,
                Stage = VersionStage.Working,
                WorkingRevision = workingRevision,
                WorkingVersion = workingVersion,
                PublishedRevision = 0,
                CreatedAt = DateTime.UtcNow
            };
            Touch(state);
            return state;
        }

        // Cập nhật DisplayVersion + UpdatedAt sau mỗi lần đổi số — state luôn tự nhất quán.
        private static void Touch(FileVersionState state)
        {
            state.DisplayVersion = FormatDisplayVersion(
                state.Stage, state.WorkingRevision, state.WorkingVersion, state.PublishedRevision);
            state.UpdatedAt = DateTime.UtcNow;
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
