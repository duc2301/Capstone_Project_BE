using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/geocoding")]
    public class GeocodingController : ControllerBase
    {
        private readonly IGeocodingService _geocoding;

        public GeocodingController(IGeocodingService geocoding)
        {
            _geocoding = geocoding;
        }

        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
        {
            var result = await _geocoding.SearchAsync(q, ct);
            return Ok(ApiResponse.Success("Geocoding completed", result));
        }
    }
}
