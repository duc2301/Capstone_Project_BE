using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.PermissionMatrix;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Domain.Enum.Cde;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    /// <summary>
    /// Ma trận phân quyền (RACI) theo dự án. Chỉ admin/PM/PA/leader mới truy cập được (service tự kiểm).
    /// GET: lưới cột (nhóm) × hàng (cây thư mục/file đã lọc theo quyền View). PUT: lưu các ô thay đổi.
    /// </summary>
    [ApiController]
    [Route("api/projects/{projectId:guid}/permission-matrix")]
    [Authorize]
    public class PermissionMatrixController : ControllerBase
    {
        private readonly IPermissionMatrixService _permissionMatrixService;

        public PermissionMatrixController(IPermissionMatrixService permissionMatrixService)
        {
            _permissionMatrixService = permissionMatrixService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMatrix(Guid projectId, [FromQuery] CdeArea? area)
        {
            var result = await _permissionMatrixService.GetMatrixAsync(
                projectId, User.GetAccountId(), User.IsAdmin(), area);
            return Ok(ApiResponse.Success("Permission matrix retrieved", result));
        }

        [HttpPut]
        public async Task<IActionResult> SaveMatrix(Guid projectId, [FromBody] SavePermissionMatrixDTO dto)
        {
            var result = await _permissionMatrixService.SaveMatrixAsync(
                projectId, dto, User.GetAccountId(), User.IsAdmin());
            return Ok(ApiResponse.Success("Permission matrix updated", result));
        }
    }
}
