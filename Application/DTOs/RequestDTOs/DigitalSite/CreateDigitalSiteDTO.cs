using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.DigitalSite
{
    public class CreateDigitalSiteDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;

        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }

        [StringLength(50)]
        public string? MapType { get; set; }
    }
}
