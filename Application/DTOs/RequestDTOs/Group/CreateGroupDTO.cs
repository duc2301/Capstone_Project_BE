using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Group
{
    public class CreateGroupDTO
    {
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }

        public Guid? OrganizationId { get; set; }
    }
}
