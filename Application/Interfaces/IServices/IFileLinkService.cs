using Application.DTOs.ResponseDTOs.FileItem;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFileLinkService
    {
        Task<RelatedFilesResponseDTO> GetRelatedFilesAsync(
            Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default);

        Task<IEnumerable<LinkableFileDTO>> GetLinkableFilesAsync(
            Guid folderId, Guid? excludeFileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default);

        Task<RelatedFilesResponseDTO> AddLinksAsync(
            Guid fileItemId, IReadOnlyCollection<Guid> relatedFileItemIds, Guid actorId, bool isSystemAdmin,
            CancellationToken ct = default);

        Task RemoveLinkAsync(
            Guid fileItemId, Guid linkedFileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default);

        Task StageLinksOnUploadAsync(
            Guid fileItemId, Folder targetFolder, IReadOnlyCollection<Guid> relatedFileItemIds,
            Guid actorId, bool isSystemAdmin, CancellationToken ct = default);

        // Kiểm phạm vi + tồn tại của file đích TRƯỚC khi upload lưu file (fail-fast, không ghi gì).
        // Cần vì hệ versioning mới commit FileItem giữa luồng -> không thể rollback file mồ côi
        // chỉ bằng 1 commit cuối; kiểm sớm để id link sai không bao giờ tạo ra file.
        Task ValidateUploadLinkTargetsAsync(
            Folder targetFolder, IReadOnlyCollection<Guid> relatedFileItemIds,
            Guid actorId, bool isSystemAdmin, CancellationToken ct = default);
    }
}
