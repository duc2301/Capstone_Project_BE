using Domain.Enum.Cde;

namespace Application.DTOs.ResponseDTOs.Folder
{
    // 1 nút trong cây thư mục CDE đã lọc theo quyền View của người gọi.
    // Không trả quyền chi tiết ở đây — quyền được kiểm tra khi user click vào folder.
    public class FolderTreeNodeDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ParentFolderId { get; set; }
        public string Name { get; set; } = null!;
        public CdeArea Area { get; set; }       
        public bool HasWarning { get; set; }

        public List<FolderTreeNodeDTO> Children { get; set; } = new();
    }
}
