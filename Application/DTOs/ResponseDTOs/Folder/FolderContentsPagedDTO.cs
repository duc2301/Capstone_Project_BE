using Application.DTOs.ResponseDTOs.FileItem;

namespace Application.DTOs.ResponseDTOs.Folder
{
    // Nội dung 1 cấp của folder khi user click trên cây.
    // Subfolder trả về TOÀN BỘ (không phân trang); chỉ file của chính folder được phân trang.
    public class FolderContentsPagedDTO : IResponseDto
    {
        // Id của chính folder được click
        public Guid Id { get; set; }

        // Toàn bộ subfolder trực tiếp đã lọc theo quyền View (không phân trang).
        public List<FolderTreeNodeDTO> Subfolders { get; set; } = new();

        // File của chính folder — phần DUY NHẤT được phân trang.
        public List<FileItemResponseDTO> Files { get; set; } = new();

        // File được cấp quyền RIÊNG (folder chứa không View được) kéo lên folder này;
        // chỉ có với user không full access, không phân trang.
        public List<FileItemResponseDTO> HoistedFiles { get; set; } = new();

        // Số subfolder (= Subfolders.Count) — tiện cho FE, không tham gia phân trang.
        public int FolderCount { get; set; }

        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        // Tổng số file CỦA FOLDER (cơ sở phân trang) — KHÔNG bao gồm HoistedFiles.
        public int TotalCount { get; set; }

        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasNextPage => PageNumber < TotalPages;
        public bool HasPreviousPage => PageNumber > 1;
    }
}
