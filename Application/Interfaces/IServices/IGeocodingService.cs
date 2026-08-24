using Application.DTOs.ResponseDTOs.Geocoding;

namespace Application.Interfaces.IServices
{
    public interface IGeocodingService
    {
        Task<GeocodeResultDTO?> SearchAsync(string query, CancellationToken ct = default);
    }
}
