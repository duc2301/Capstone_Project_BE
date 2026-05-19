using Domain.Enum.Cde;

namespace Application.DTOs.ResponseDTOs.Folder
{
    public class FolderResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ParentFolderId { get; set; }
        public string Name { get; set; } = null!;
        public CdeArea Area { get; set; }
        public Guid? OwnerOrganizationId { get; set; }
        public bool IsTemplate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
