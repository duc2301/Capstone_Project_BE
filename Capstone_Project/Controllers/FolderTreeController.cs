using Application.DTOs.ApiResponseDTO;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Domain.Enum.Cde;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/folder-tree")]
    [Authorize]
    public class FolderTreeController : ControllerBase
    {
        private readonly IFolderTreeService _folderTreeService;

        public FolderTreeController(IFolderTreeService folderTreeService)
        {
            _folderTreeService = folderTreeService;
        }

        // Cây thư mục CDE khi user vào dự án: chỉ trả về các folder mà người gọi có quyền View.
        // Lọc theo khu vực qua ?area=Wip|Shared|Published|Archived (tùy chọn).
        [HttpGet("tree")]
        public async Task<IActionResult> GetTree([FromQuery] Guid projectId, [FromQuery] CdeArea? area)
        {
            var tree = await _folderTreeService.GetTreeAsync(projectId, User.GetAccountId(), User.IsAdmin(), area);
            return Ok(ApiResponse.Success("CDE tree retrieved", tree));
        }

        // Danh sách file khi user click vào 1 folder; quyền View được kiểm tra tại đây.
        [HttpGet("folders/{folderId:guid}/files")]
        public async Task<IActionResult> GetFilesInFolder(Guid folderId)
        {
            var files = await _folderTreeService.GetFilesByFolderAsync(folderId, User.GetAccountId(), User.IsAdmin());
            return Ok(ApiResponse.Success("Folder files retrieved", files));
        }
    }
}
