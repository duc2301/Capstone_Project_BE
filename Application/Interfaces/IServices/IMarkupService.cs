using Application.DTOs.RequestDTOs.Markup;
using Application.DTOs.ResponseDTOs.Markup;
using Domain.Enum.File;

namespace Application.Interfaces.IServices
{
    public interface IMarkupService
    {
        Task<MarkupSetResponseDTO> CreateSetAsync(CreateMarkupSetDTO dto, Guid actorId, CancellationToken ct = default);
        Task<IEnumerable<MarkupSetResponseDTO>> GetSetsByFileAsync(Guid fileItemId, Guid actorId, CancellationToken ct = default);
        Task<IEnumerable<MarkupSetResponseDTO>> GetSetsByIssueAsync(Guid issueId, Guid actorId, CancellationToken ct = default);
        Task<MarkupSetResponseDTO> GetSetDetailAsync(Guid setId, Guid actorId, CancellationToken ct = default);
        Task<MarkupSetResponseDTO> UpdateSetStatusAsync(Guid setId, MarkupSetStatus status, Guid actorId, CancellationToken ct = default);
        Task<MarkupSetResponseDTO> LinkToIssueAsync(Guid setId, Guid? issueId, Guid actorId, CancellationToken ct = default);
        Task<FileNoteResponseDTO> AddNoteAsync(Guid setId, CreateFileNoteDTO dto, Guid actorId, CancellationToken ct = default);
        Task<FileNoteResponseDTO> UpdateNoteAsync(Guid noteId, UpdateFileNoteDTO dto, Guid actorId, CancellationToken ct = default);
        Task DeleteNoteAsync(Guid noteId, Guid actorId, CancellationToken ct = default);
    }
}
