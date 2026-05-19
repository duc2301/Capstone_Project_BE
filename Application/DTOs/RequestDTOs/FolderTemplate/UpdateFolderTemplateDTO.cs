using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.FolderTemplate
{
    public class UpdateFolderTemplateDTO
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public string? StructureJson { get; set; }
    }
}
