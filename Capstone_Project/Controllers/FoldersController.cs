using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Domain.Enum.Cde;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/folders")]
    [Authorize]
    public class FoldersController : ControllerBase
    {
        private readonly IFolderService _service;
        private readonly IFolderPermissionService _permission;
        private readonly IFolderBootstrapService _bootstrap;
        private readonly IFolderTransitionService _transition;
        private readonly ICurrentUserService _currentUser;

        public FoldersController(
            IFolderService service,
            IFolderPermissionService permission,
            IFolderBootstrapService bootstrap,
            IFolderTransitionService transition,
            ICurrentUserService currentUser)
        {
            _service = service;
            _permission = permission;
            _bootstrap = bootstrap;
            _transition = transition;
            _currentUser = currentUser;
        }

        // Cây thư mục CDE của 1 dự án, đã lọc theo quyền View của người gọi.
        // Lọc theo khu vực qua ?area=Wip|Shared|Published|Archived (tùy chọn).
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] Guid projectId, [FromQuery] CdeArea? area)
        {
            var actor = RequireActor();
            var tree = await _permission.GetTreeAsync(projectId, actor, area);
            return Ok(ApiResponse.Success("CDE tree retrieved", tree));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var actor = RequireActor();
            await _permission.RequireAsync(actor, id, FolderAction.View);
            return Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));
        }

        // Team Leader (hoặc PM/Admin) tạo thư mục con để tổ chức file.
        // Area/OwnerGroup kế thừa từ folder cha. 4 khu vực gốc do hệ thống tự tạo.
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubFolderDTO dto)
        {
            var result = await _bootstrap.CreateChildFolderAsync(dto.ParentFolderId, dto.Name);
            return Ok(ApiResponse.Success("Created successfully", result));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderDTO dto)
        {
            var actor = RequireActor();
            await _permission.RequireAsync(actor, id, FolderAction.Edit);
            return Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var actor = RequireActor();
            await _permission.RequireAsync(actor, id, FolderAction.Edit);
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }

        // Chuyển trạng thái CDE cả thư mục (đệ quy): Wip→Shared→Published→Archived (tiến 1 bậc).
        [HttpPost("{id:guid}/promote")]
        public async Task<IActionResult> Promote(Guid id, [FromBody] PromoteFolderDTO dto)
        {
            var result = await _transition.PromoteFolderAsync(id, dto.TargetArea);
            return Ok(ApiResponse.Success("Folder promoted", result));
        }

        // Quyền hiệu lực của chính người gọi trên 1 folder.
        [HttpGet("{id:guid}/permissions/me")]
        public async Task<IActionResult> GetMyPermission(Guid id)
        {
            var actor = RequireActor();
            var perm = await _permission.EvaluateAsync(actor, id);
            return Ok(ApiResponse.Success("Effective permission retrieved", perm));
        }

        // ACL override tường minh (Admin/PM). Liệt kê / set / xóa.
        [HttpGet("{id:guid}/permissions")]
        public async Task<IActionResult> GetPermissions(Guid id)
            => Ok(ApiResponse.Success("Permissions retrieved", await _permission.GetPermissionsAsync(id)));

        [HttpPut("{id:guid}/permissions")]
        public async Task<IActionResult> SetPermission(Guid id, [FromBody] SetFolderPermissionDTO dto)
            => Ok(ApiResponse.Success("Permission saved", await _permission.SetPermissionAsync(id, dto)));

        [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
        public async Task<IActionResult> DeletePermission(Guid id, Guid permissionId)
        {
            await _permission.DeletePermissionAsync(id, permissionId);
            return Ok(ApiResponse.Success("Permission removed"));
        }

        private Guid RequireActor()
            => _currentUser.AccountId
               ?? throw new ApiExceptionResponse("Authentication required.", 401);
    }
}
