using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Group
{
    public class UpdateGroupDTO
    {
        [StringLength(150)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public Guid? OrganizationId { get; set; }
    }
}
