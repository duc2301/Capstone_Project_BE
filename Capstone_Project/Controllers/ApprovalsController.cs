using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Approval;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    /// <summary>
    /// API quản lý yêu cầu phê duyệt file CDE.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/approvals")]
    public class ApprovalsController : ControllerBase
    {
        private readonly IApprovalService _approvalService;

        public ApprovalsController(IApprovalService approvalService)
        {
            _approvalService = approvalService;
        }

        /// <summary>
        /// Xem tất cả yêu cầu phê duyệt mà người dùng hiện tại được phép xem.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(ApiResponse.Success("Approvals retrieved", await _approvalService.GetAllAsync()));

        /// <summary>
        /// Xem danh sách yêu cầu đang chờ duyệt của Team Leader hiện tại.
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
            => Ok(ApiResponse.Success("Pending approvals retrieved", await _approvalService.GetPendingAsync()));

        /// <summary>
        /// Xem chi tiết một yêu cầu phê duyệt.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success("Approval request retrieved", await _approvalService.GetByIdAsync(id)));

        /// <summary>
        /// Duyệt file đang chờ phê duyệt.
        /// </summary>
        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve(Guid id)
            => Ok(ApiResponse.Success("File approved", await _approvalService.ApproveAsync(id)));

        /// <summary>
        /// Từ chối file đang chờ phê duyệt.
        /// </summary>
        /// <remarks>Lý do từ chối là bắt buộc.</remarks>
        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectApprovalRequestDTO dto)
            => Ok(ApiResponse.Success("File rejected", await _approvalService.RejectAsync(id, dto)));
    }
}
