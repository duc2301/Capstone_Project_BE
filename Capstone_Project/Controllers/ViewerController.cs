using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/viewer")]
    public class ViewerController : ControllerBase
    {
        private readonly IViewerService _viewer;

        public ViewerController(IViewerService viewer)
        {
            _viewer = viewer;
        }

        [HttpGet("token")]
        [Authorize]
        public async Task<IActionResult> GetToken(CancellationToken ct)
            => Ok(ApiResponse.Success("OK", await _viewer.GetViewerTokenAsync(ct)));

        [HttpPost("models")]
        [Authorize]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(524_288_000)]                                   
        [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
        public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Tệp trống."));

            using var stream = file.OpenReadStream();
            var result = await _viewer.UploadAndTranslateAsync(stream, file.FileName, ct);
            return Ok(ApiResponse.Success("OK", result));
        }

        [HttpGet("models/{urn}/status")]
        [Authorize]
        public async Task<IActionResult> Status(string urn, CancellationToken ct)
            => Ok(ApiResponse.Success("OK", await _viewer.GetStatusAsync(urn, ct)));
    }
}
