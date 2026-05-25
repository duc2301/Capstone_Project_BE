using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.ProjectModel
{
    public class UpdateProjectModelDTO
    {
        [StringLength(250)]
        public string? Name { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }
    }
}
