using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Audit;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/audit-logs")]
    [ApiController]
    [Authorize]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        // Nhật ký toàn hệ thống (mọi dự án) — chỉ Admin hệ thống.
        // Lọc về 1 dự án bằng query ?projectId=
        [HttpGet("system")]
        public async Task<IActionResult> GetSystem([FromQuery] AuditLogFilterDTO filter)
        {
            var result = await _auditLogService.GetSystemAsync(filter, User.IsAdmin());
            return Ok(ApiResponse.Success("Audit logs retrieved", result));
        }

        // Toàn bộ nhật ký của 1 dự án — Admin hệ thống hoặc PM/ProjectAdmin của dự án đó.
        [HttpGet("projects/{projectId:guid}")]
        public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] AuditLogFilterDTO filter)
        {
            var result = await _auditLogService.GetByProjectAsync(
                projectId, filter, User.GetAccountId(), User.IsAdmin());
            return Ok(ApiResponse.Success("Project audit logs retrieved", result));
        }

        // Nhật ký mà user hiện tại ĐƯỢC PHÉP thấy trong dự án:
        // chỉ log của folder user có quyền View, hoặc log của nhóm user tham gia.
        [HttpGet("projects/{projectId:guid}/my")]
        public async Task<IActionResult> GetMine(Guid projectId, [FromQuery] AuditLogFilterDTO filter)
        {
            var result = await _auditLogService.GetMyInProjectAsync(
                projectId, filter, User.GetAccountId());
            return Ok(ApiResponse.Success("Audit logs retrieved", result));
        }
    }
}
