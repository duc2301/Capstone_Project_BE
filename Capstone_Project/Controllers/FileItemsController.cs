using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Approval;
using Application.DTOs.RequestDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/file-items")]
    [Authorize]
    public class FileItemsController : ControllerBase
    {
        private readonly IFileItemService _service;
        private readonly IFileUploadService _upload;
        private readonly IApprovalService _approval;
        private readonly IZoneReturnRequestService _zoneReturnRequestService;
        private readonly IFileViewService _view;
        private readonly IFileSignaturePositionService _signaturePosition;
        private readonly IPdfSignatureService _pdfSignature;

        public FileItemsController(
            IFileItemService service,
            IFileUploadService upload,
            IApprovalService approval,
            IZoneReturnRequestService zoneReturnRequestService,
            IFileViewService view,
            IFileSignaturePositionService signaturePosition,
            IPdfSignatureService pdfSignature)
        {
            _service = service;
            _upload = upload;
            _approval = approval;
            _zoneReturnRequestService = zoneReturnRequestService;
            _view = view;
            _signaturePosition = signaturePosition;
            _pdfSignature = pdfSignature;
        }

        // Luồng tải file lên (multipart/form-data): file + FolderId + FileType + (Name tùy chọn).
        // Cho phép file lớn tới 500MB (CAD/BIM .nwd/.rvt...) — mặc định Kestrel ~28MB, multipart ~128MB.
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(524_288_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
        public async Task<IActionResult> Upload([FromForm] UploadFileDTO dto, IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0)
                throw new ApiExceptionResponse("No file provided.", 400);

            await using var stream = file.OpenReadStream();
            var result = await _upload.UploadAsync(dto, stream, file.FileName, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("Uploaded successfully", result));
        }

        // Tải file về (kiểm tra quyền Download trong service).
        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id, CancellationToken ct)
        {
            var dl = await _upload.OpenDownloadAsync(id, User.GetAccountId(), ct);
            return File(dl.Content, dl.ContentType, dl.FileName);
        }

        // Link xem/tải tạm thời (pre-signed). null nếu lưu local -> dùng /download. ?minutes= để chỉnh hạn.
        [HttpGet("{id:guid}/url")]
        public async Task<IActionResult> GetUrl(Guid id, CancellationToken ct, [FromQuery] int minutes = 60)
        {
            var url = await _upload.GetViewUrlAsync(id, User.GetAccountId(), minutes, ct);
            return Ok(ApiResponse.Success("File URL", new { url }));
        }

        // "Xem chi tiết": FE dựa vào Kind để hiển thị (model = APS viewer, inline = web, download = tải về).
        [HttpGet("{id:guid}/view")]
        public async Task<IActionResult> GetViewInfo(Guid id, CancellationToken ct)
            => Ok(ApiResponse.Success("File view info", await _view.GetViewInfoAsync(id, User.GetAccountId(), ct)));

        // Bytes PDF hiệu dụng (PDF gốc / Office đã convert) — FE render bằng pdf.js để markup 2D theo trang. Same-origin.
        [HttpGet("{id:guid}/view-pdf")]
        public async Task<IActionResult> GetViewPdf(Guid id, CancellationToken ct)
        {
            var pdf = await _view.OpenViewPdfAsync(id, User.GetAccountId(), ct);
            return File(pdf.Content, "application/pdf", pdf.FileName);
        }

        // Dịch lại model (IFC/CAD) lên APS — dùng khi trạng thái dịch là Failed. Chạy ở hàng đợi nền.
        [HttpPost("{id:guid}/retranslate")]
        public async Task<IActionResult> Retranslate(Guid id, CancellationToken ct)
        {
            await _view.RetranslateAsync(id, User.GetAccountId(), ct);
            return Ok(ApiResponse.Success("Model re-translation queued"));
        }

        /// <summary>
        /// Gửi file CDE để chờ Team Leader phê duyệt.
        /// </summary>
        /// <remarks>
        /// Chỉ member active trong team/project của file mới được gửi duyệt.
        /// </remarks>
        [HttpPost("{id:guid}/submit-approval")]
        public async Task<IActionResult> SubmitApproval(Guid id, [FromBody] SubmitApprovalRequestDTO? dto)
            => Ok(ApiResponse.Success("File submitted for approval", await _approval.SubmitAsync(id, dto, User.GetAccountId())));

        /// <summary>
        /// Chuyen file sang zone CDE khac. Chi active Team Leader cua team so huu file moi duoc thuc hien.
        /// </summary>
        [HttpPost("{fileId:guid}/transfer-zone")]
        public async Task<IActionResult> TransferZone(Guid fileId, [FromBody] TransferZoneRequestDTO dto)
            => Ok(ApiResponse.Success("File zone transferred", await _service.TransferZoneAsync(fileId, dto, User.GetAccountId())));

        [HttpPost("{fileId:guid}/return-requests")]
        public async Task<IActionResult> CreateReturnRequest(Guid fileId, [FromBody] CreateZoneReturnRequestDTO dto)
            => Ok(await _zoneReturnRequestService.CreateAsync(fileId, dto, User.GetAccountId()));

        /// <summary>
        /// Luu vi tri dat chu ky truc quan tren PDF (chi PDF, chi khi file dang o WIP).
        /// </summary>
        [HttpPost("{fileId:guid}/signature-position")]
        public async Task<IActionResult> SaveSignaturePosition(Guid fileId, [FromBody] SaveSignaturePositionDTO dto)
            => Ok(ApiResponse.Success(
                "Signature position saved",
                await _signaturePosition.SaveAsync(fileId, dto, User.GetAccountId())));

        /// <summary>
        /// Lay vi tri dat chu ky truc quan da luu cua file.
        /// </summary>
        [HttpGet("{fileId:guid}/signature-position")]
        public async Task<IActionResult> GetSignaturePosition(Guid fileId)
            => Ok(ApiResponse.Success("Signature position retrieved", await _signaturePosition.GetAsync(fileId)));

        /// <summary>
        /// Kich thuoc trang PDF thuc te (points) -> FE dung de tinh ty le dat vi tri ky, tranh gia dinh A4 co dinh.
        /// </summary>
        [HttpGet("{fileId:guid}/pdf-page-info")]
        public async Task<IActionResult> GetPdfPageInfo(Guid fileId, [FromQuery] int pageNumber = 1)
            => Ok(ApiResponse.Success("PDF page info retrieved", await _signaturePosition.GetPageInfoAsync(fileId, pageNumber)));

        /// <summary>
        /// Lay thong tin (metadata) ban PDF da ky truc quan. Tai noi dung file qua endpoint /download hien co.
        /// </summary>
        [HttpGet("{fileId:guid}/signed-file")]
        public async Task<IActionResult> GetSignedFile(Guid fileId)
            => Ok(await _pdfSignature.GetSignedFileInfoAsync(fileId, User.GetAccountId()));

        // Danh sách file trong 1 folder (FE gọi khi mở/chọn folder).
        [HttpGet("by-folder/{folderId:guid}")]
        public async Task<IActionResult> GetByFolder(Guid folderId)
            => Ok(ApiResponse.Success("Files retrieved", await _service.GetByFolderAsync(folderId, User.GetAccountId())));

        // Tất cả phiên bản của 1 file.
        [HttpGet("{id:guid}/versions")]
        public async Task<IActionResult> GetVersions(Guid id)
            => Ok(ApiResponse.Success("Versions retrieved", await _service.GetVersionsAsync(id, User.GetAccountId())));

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetAllAsync()));

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
            => Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFileItemDTO dto)
            => Ok(ApiResponse.Success("Created successfully", await _service.CreateAsync(dto)));

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFileItemDTO dto)
            => Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }
    }
}
