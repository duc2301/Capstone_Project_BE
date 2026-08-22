using System.Text.Json;
using Application.DTOs.ResponseDTOs.Geocoding;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Geo
{
    public class OpenStreetMapGeocodingService : IGeocodingService
    {
        public const string DefaultUserAgent = "BIM-CDE-Portal/1.0 (capstone SU26SE017)";

        private const string NominatimProvider = "nominatim";
        private const string PhotonProvider = "photon";
        private const string DefaultNominatimBaseUrl = "https://nominatim.osm.org";
        private const string DefaultPhotonBaseUrl = "https://photon.komoot.io";
        private const string Language = "vi";
        private const int MaxQueryLength = 300;

        private static readonly string[] PhotonNameKeys =
            { "name", "street", "district", "city", "state", "country" };

        private readonly HttpClient _http;
        private readonly ILogger<OpenStreetMapGeocodingService> _logger;
        private readonly string _nominatimBaseUrl;
        private readonly string _photonBaseUrl;

        public OpenStreetMapGeocodingService(
            HttpClient http,
            IConfiguration config,
            ILogger<OpenStreetMapGeocodingService> logger)
        {
            _http = http;
            _logger = logger;
            _nominatimBaseUrl = config["Geocoding:NominatimBaseUrl"] ?? DefaultNominatimBaseUrl;
            _photonBaseUrl = config["Geocoding:PhotonBaseUrl"] ?? DefaultPhotonBaseUrl;
        }

        public async Task<GeocodeResultDTO?> SearchAsync(string query, CancellationToken ct = default)
        {
            var q = query?.Trim() ?? string.Empty;
            if (q.Length == 0)
                throw new ApiExceptionResponse("Chưa nhập địa chỉ cần định vị.", 400);
            if (q.Length > MaxQueryLength)
                q = q[..MaxQueryLength];

            var encoded = Uri.EscapeDataString(q);
            var anyProviderAnswered = false;

            var nominatim = await TryProviderAsync(
                NominatimProvider,
                $"{_nominatimBaseUrl}/search?format=jsonv2&limit=1&accept-language={Language}&q={encoded}",
                ParseNominatim,
                ct);
            anyProviderAnswered |= nominatim.Answered;
            if (nominatim.Result is not null) return nominatim.Result;

            var photon = await TryProviderAsync(
                PhotonProvider,
                $"{_photonBaseUrl}/api/?limit=1&q={encoded}",
                ParsePhoton,
                ct);
            anyProviderAnswered |= photon.Answered;
            if (photon.Result is not null) return photon.Result;

            if (!anyProviderAnswered)
                throw new ApiExceptionResponse(
                    "Không kết nối được dịch vụ bản đồ. Hãy kiểm tra kết nối mạng hoặc nhập toạ độ thủ công.", 503);

            return null;
        }

        private async Task<(bool Answered, GeocodeResultDTO? Result)> TryProviderAsync(
            string provider,
            string url,
            Func<JsonElement, GeocodeResultDTO?> parse,
            CancellationToken ct)
        {
            try
            {
                using var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Geocoding provider {Provider} trả {Status}.", provider, (int)response.StatusCode);
                    return (false, null);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                var result = parse(document.RootElement);
                if (result is not null) result.Provider = provider;
                return (true, result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Geocoding provider {Provider} không gọi được.", provider);
                return (false, null);
            }
        }

        private static GeocodeResultDTO? ParseNominatim(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return null;

            var first = root[0];
            if (!first.TryGetProperty("lat", out var latText) || !first.TryGetProperty("lon", out var lonText))
                return null;
            if (!TryParseCoordinate(latText.GetString(), out var lat)) return null;
            if (!TryParseCoordinate(lonText.GetString(), out var lng)) return null;

            return new GeocodeResultDTO
            {
                Lat = lat,
                Lng = lng,
                DisplayName = first.TryGetProperty("display_name", out var name)
                    ? name.GetString() ?? string.Empty
                    : string.Empty
            };
        }

        private static GeocodeResultDTO? ParsePhoton(JsonElement root)
        {
            if (!root.TryGetProperty("features", out var features)
                || features.ValueKind != JsonValueKind.Array
                || features.GetArrayLength() == 0)
                return null;

            var first = features[0];
            if (!first.TryGetProperty("geometry", out var geometry)
                || !geometry.TryGetProperty("coordinates", out var coordinates)
                || coordinates.ValueKind != JsonValueKind.Array
                || coordinates.GetArrayLength() < 2)
                return null;

            return new GeocodeResultDTO
            {
                Lat = coordinates[1].GetDouble(),
                Lng = coordinates[0].GetDouble(),
                DisplayName = first.TryGetProperty("properties", out var properties)
                    ? BuildPhotonName(properties)
                    : string.Empty
            };
        }

        private static string BuildPhotonName(JsonElement properties)
        {
            var parts = new List<string>();
            foreach (var key in PhotonNameKeys)
            {
                if (!properties.TryGetProperty(key, out var value)) continue;
                var text = value.GetString()?.Trim();
                if (!string.IsNullOrEmpty(text) && !parts.Contains(text, StringComparer.OrdinalIgnoreCase))
                    parts.Add(text);
            }
            return string.Join(", ", parts);
        }

        private static bool TryParseCoordinate(string? text, out double value) =>
            double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
