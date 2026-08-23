namespace Application.DTOs.ResponseDTOs.Geocoding
{
    public class GeocodeResultDTO
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
    }
}
