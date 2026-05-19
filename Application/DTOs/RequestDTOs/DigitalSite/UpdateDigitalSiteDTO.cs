using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.DigitalSite
{
    public class UpdateDigitalSiteDTO
    {
        [StringLength(250)]
        public string? Name { get; set; }

        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }

        [StringLength(50)]
        public string? MapType { get; set; }
    }
}
