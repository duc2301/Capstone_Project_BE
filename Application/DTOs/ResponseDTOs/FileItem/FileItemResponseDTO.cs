using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class FileItemResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FolderId { get; set; }
        public string Name { get; set; } = null!;
        public FileType FileType { get; set; }
        public FileItemStatus Status { get; set; }
        public Guid? CurrentVersionId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
