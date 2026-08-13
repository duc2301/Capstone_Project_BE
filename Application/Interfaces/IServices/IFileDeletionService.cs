using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.Interfaces.IServices
{
    public interface IFileDeletionService
    {
        Task<DeleteFileResultDTO> DeleteFlaggedAsync(
            Guid fileItemId, Guid actorId, bool isSystemAdmin, CancellationToken ct = default);
    }
}
