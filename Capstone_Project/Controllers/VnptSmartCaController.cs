using Application.DTOs.RequestDTOs.SmartCA;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    /// <summary>
    /// API thao tac VNPT SmartCA cho approval request can ky so.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/approvals/{approvalId:guid}/vnpt-smartca")]
    public class VnptSmartCaController : ControllerBase
    {
        private readonly IVnptSmartCaService _vnptSmartCaService;

        public VnptSmartCaController(IVnptSmartCaService vnptSmartCaService)
        {
            _vnptSmartCaService = vnptSmartCaService;
        }

        /// <summary>
        /// Leader lay danh sach chung thu so VNPT SmartCA cua user ky.
        /// </summary>
        [HttpPost("certificates")]
        public async Task<IActionResult> GetCertificates(
            Guid approvalId,
            [FromBody] GetCertificateRequestDto request)
            => Ok(await _vnptSmartCaService.GetCertificatesAsync(
                approvalId,
                request,
                User.GetAccountId()));

        /// <summary>
        /// Leader tao giao dich ky so VNPT SmartCA cho file dang cho duyet.
        /// </summary>
        [HttpPost("sign-request")]
        public async Task<IActionResult> SendSignRequest(
            Guid approvalId,
            [FromBody] SendSignRequestDto request)
            => Ok(await _vnptSmartCaService.SendSignRequestAsync(
                approvalId,
                request,
                User.GetAccountId()));

        /// <summary>
        /// Leader kiem tra trang thai giao dich ky so tren VNPT SmartCA.
        /// </summary>
        [HttpGet("transaction-status/{transactionId}")]
        public async Task<IActionResult> GetTransactionStatus(
            Guid approvalId,
            string transactionId)
            => Ok(await _vnptSmartCaService.GetTransactionStatusAsync(
                approvalId,
                transactionId,
                User.GetAccountId()));

        /// <summary>
        /// Leader xem thong tin giao dich ky so da luu trong approval.
        /// </summary>
        [HttpGet("signature")]
        public async Task<IActionResult> GetApprovalSignature(Guid approvalId)
            => Ok(await _vnptSmartCaService.GetApprovalSignatureAsync(
                approvalId,
                User.GetAccountId()));
    }
}
