using Domain.Enum.Cde;

namespace Application.DTOs.RequestDTOs.PermissionMatrix
{
    // Bộ lọc tìm kiếm cho ma trận phân quyền (GET). Tất cả rỗng/null = không lọc (giữ hành vi cũ).
    // Bind từ query string: ?area=1&groupIds=..&groupIds=..&folderIds=..&fileIds=..
    // - Area  : vùng CDE (WIP/Shared/Published/Archived) — 1 giá trị.
    // - GroupIds/FolderIds/FileIds : chọn nhiều (multi-select theo Id).
    //   Lọc cột theo GroupId; lọc hàng theo folder/file. Lọc quyền (permission level) tạm hoãn.
    public class PermissionMatrixFilterDTO
    {
        public CdeArea? Area { get; set; }
        public List<Guid>? GroupIds { get; set; }
        public List<Guid>? FolderIds { get; set; }
        public List<Guid>? FileIds { get; set; }

        // Có áp bất kỳ bộ lọc hàng (folder/file) nào không.
        public bool HasRowFilter =>
            (FolderIds is { Count: > 0 }) || (FileIds is { Count: > 0 });
    }
}
