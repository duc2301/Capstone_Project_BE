using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.DTOs.ResponseDTOs.Folder
{
    // Nội dung 1 cấp của folder khi user click trên cây:
    // chỉ subfolder trực tiếp (đã lọc theo quyền View) + file của chính folder đó.
    public class FolderContentsDTO : IResponseDto
    {
        // Id của chính folder được click
        public Guid Id { get; set; }
        public List<FolderTreeNodeDTO> Subfolders { get; set; } = new();
        public List<FileItemResponseDTO> Files { get; set; } = new();
    }
}
