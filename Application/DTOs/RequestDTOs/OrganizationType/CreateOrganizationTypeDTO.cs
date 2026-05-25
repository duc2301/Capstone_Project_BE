using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.OrganizationType
{
    public class CreateOrganizationTypeDTO
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = null!;

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
