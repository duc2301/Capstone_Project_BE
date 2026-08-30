using System.ComponentModel.DataAnnotations;
using Domain.Enum.Cde;

namespace Application.DTOs.RequestDTOs.Folder
{
    public class CreateFolderDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        public Guid? ParentFolderId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        [Required]
        public CdeArea Area { get; set; }

        public Guid? OwnerOrganizationId { get; set; }
        public bool IsTemplate { get; set; }
    }
}
