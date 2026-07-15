using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Folder;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
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
        //private readonly IFolderPermissionServiceOld _permission;
        private readonly IFolderBootstrapService _bootstrap;

        public FoldersController(
            IFolderService service,
            //IFolderPermissionServiceOld permission,
            IFolderBootstrapService bootstrap)
        {
            _service = service;
            //_permission = permission;
            _bootstrap = bootstrap;
        }

        // Cây thư mục CDE của 1 dự án, đã lọc theo quyền View của người gọi.
        // Lọc theo khu vực qua ?area=Wip|Shared|Published|Archived (tùy chọn).
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] Guid projectId, [FromQuery] CdeArea? area)
        {
            var tree = true;
            return Ok(ApiResponse.Success("CDE tree retrieved", tree));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            //await _permission.RequireAsync(User.GetAccountId(), id, FolderAction.View);
            return Ok(ApiResponse.Success("Retrieved successfully", await _service.GetByIdAsync(id)));
        }

        // Team Leader (hoặc PM/Admin) tạo thư mục con để tổ chức file.
        // Area/OwnerGroup kế thừa từ folder cha. 4 khu vực gốc do hệ thống tự tạo.
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubFolderDTO dto)
        {
            var result = await _bootstrap.CreateChildFolderAsync(
                dto.ParentFolderId, dto.Name, User.GetAccountId(), User.GetSystemRole());
            return Ok(ApiResponse.Success("Created successfully", result));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderDTO dto)
        {
            //await _permission.RequireAsync(User.GetAccountId(), id, FolderAction.Edit);
            return Ok(ApiResponse.Success("Updated successfully", await _service.UpdateAsync(id, dto)));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            //await _permission.RequireAsync(User.GetAccountId(), id, FolderAction.Edit);
            await _service.DeleteAsync(id);
            return Ok(ApiResponse.Success("Deleted successfully"));
        }

        //// Quyền hiệu lực của chính người gọi trên 1 folder.
        //[HttpGet("{id:guid}/permissions/me")]
        //public async Task<IActionResult> GetMyPermission(Guid id)
        //{
        //    //var perm = await _permission.EvaluateAsync(User.GetAccountId(), id);
        //    return Ok(ApiResponse.Success("Effective permission retrieved", true));
        //}

        //// ACL override tường minh (Admin/PM). Liệt kê / set / xóa.
        //[HttpGet("{id:guid}/permissions")]
        //public async Task<IActionResult> GetPermissions(Guid id)
        //    //=> Ok(ApiResponse.Success("Permissions retrieved",
        //    //    await _permission.GetPermissionsAsync(id, User.GetAccountId(), User.GetSystemRole())));
        //    => Ok(ApiResponse.Success("Permissions retrieved", true));


        //[HttpPut("{id:guid}/permissions")]
        //public async Task<IActionResult> SetPermission(Guid id, [FromBody] SetFolderPermissionDTO dto)
        //    //=> Ok(ApiResponse.Success("Permission saved",
        //    //    await _permission.SetPermissionAsync(id, dto, User.GetAccountId(), User.GetSystemRole())));
        //    => Ok(ApiResponse.Success("Permissions retrieved", true));

        //[HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
        //public async Task<IActionResult> DeletePermission(Guid id, Guid permissionId)
        //{
        //    //await _permission.DeletePermissionAsync(id, permissionId, User.GetAccountId(), User.GetSystemRole());
        //    return Ok(ApiResponse.Success("Permission removed"));
        //}
    }
}
