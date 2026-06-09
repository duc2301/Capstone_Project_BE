using System.ComponentModel.DataAnnotations;
using Domain.Enum.Cde;

namespace Application.DTOs.RequestDTOs.Folder
{
    public class UpdateFolderDTO
    {
        public Guid? ParentFolderId { get; set; }

        [StringLength(250)]
        public string? Name { get; set; }

        public CdeArea? Area { get; set; }
        public Guid? OwnerOrganizationId { get; set; }
        public Guid? OwnerGroupId { get; set; }
        public bool? IsTemplate { get; set; }
    }
}
