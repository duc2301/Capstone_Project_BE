namespace Application.DTOs.ResponseDTOs.FileItem
{
    // Kich thuoc trang PDF thuc te (points) -> FE dung de tinh ty le thay vi gia dinh co dinh A4.
    public class PdfPageInfoResponseDTO
    {
        public Guid FileItemId { get; set; }
        public int PageNumber { get; set; }
        public int PageCount { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string? PreviewUrl { get; set; }
    }
}
