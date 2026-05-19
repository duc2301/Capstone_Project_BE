using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.FolderTemplate
{
    public class CreateFolderTemplateDTO
    {
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public string StructureJson { get; set; } = null!;
    }
}
