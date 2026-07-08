namespace Application.DTOs.ResponseDTOs.FileItem
{
    public class FileSignaturePositionResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public int PageNumber { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
