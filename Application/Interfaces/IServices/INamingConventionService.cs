using Application.DTOs.RequestDTOs.NamingConvention;
using Application.DTOs.ResponseDTOs.NamingConvention;

namespace Application.Interfaces.IServices
{
    // Toàn bộ business logic của naming convention: cấu hình (admin), payload cho dialog upload,
    // validate lựa chọn của user và sinh tên file. Upload flow chỉ được gọi qua interface này.
    public interface INamingConventionService
    {
        // --- Cấu hình (admin) ---
        Task<NamingConventionResponseDTO> CreateAsync(CreateNamingConventionDTO dto, Guid actor);
        Task<NamingConventionResponseDTO> GetByIdAsync(Guid id);
        Task<IEnumerable<NamingConventionResponseDTO>> GetByProjectAsync(Guid projectId);
        Task<NamingConventionResponseDTO> UpdateAsync(Guid id, UpdateNamingConventionDTO dto);
        Task DeleteAsync(Guid id);

        Task<NamingConventionResponseDTO> AddFieldAsync(Guid conventionId, CreateNamingFieldDTO dto, Guid actor);
        Task<NamingConventionResponseDTO> UpdateFieldAsync(Guid fieldId, UpdateNamingFieldDTO dto);
        Task DeleteFieldAsync(Guid fieldId);

        Task<NamingConventionResponseDTO> AddFieldValuesAsync(Guid fieldId, List<CreateNamingFieldValueDTO> dtos, Guid actor);
        Task<NamingConventionResponseDTO> UpdateFieldValueAsync(Guid valueId, UpdateNamingFieldValueDTO dto);
        Task DeleteFieldValueAsync(Guid valueId);

        Task<NamingConventionResponseDTO> SetLockedValueAsync(Guid fieldId, SetLockedValueDTO dto, Guid actor);
        Task<NamingConventionResponseDTO> RemoveLockedValueAsync(Guid fieldId);

        // --- Gán folder ---
        Task<NamingConventionResponseDTO> AssignFoldersAsync(Guid conventionId, AssignFoldersDTO dto);
        Task UnassignFolderAsync(Guid folderId);

        // --- Upload flow ---
        // Payload cho dialog upload: convention đang áp cho folder (hoặc HasNamingConvention = false).
        Task<FolderNamingConventionResponseDTO> GetByFolderAsync(Guid folderId);

        // Validate lựa chọn + tự chèn locked values + ghép tên theo delimiter, giữ nguyên đuôi file.
        // selectionsJson: JSON array [{"fieldId":"...","valueId":"..."}] từ form upload (có thể null).
        Task<FileNameGenerationResultDTO> GenerateFileNameAsync(
            Guid folderId, string? selectionsJson, string originalFileName, CancellationToken ct = default);

        // Stage các dòng FileNamingMetadata cho file vừa tạo (KHÔNG SaveChanges —
        // upload flow commit chung trong transaction của nó).
        Task StageFileNamingMetadataAsync(Guid fileItemId, FileNameGenerationResultDTO generation);
    }
}
