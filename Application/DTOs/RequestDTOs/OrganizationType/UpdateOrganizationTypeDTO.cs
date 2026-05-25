using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.OrganizationType
{
    public class UpdateOrganizationTypeDTO
    {
        [StringLength(50)]
        public string? Code { get; set; }

        [StringLength(150)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool? IsActive { get; set; }
    }
}
