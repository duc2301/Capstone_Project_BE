using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
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
    }
}
