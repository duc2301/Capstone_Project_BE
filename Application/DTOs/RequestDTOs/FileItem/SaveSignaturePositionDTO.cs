using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.FileItem
{
    public class SaveSignaturePositionDTO
    {
        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; }

        public float X { get; set; }
        public float Y { get; set; }

        [Range(0.01, float.MaxValue)]
        public float Width { get; set; }

        [Range(0.01, float.MaxValue)]
        public float Height { get; set; }
    }
}
