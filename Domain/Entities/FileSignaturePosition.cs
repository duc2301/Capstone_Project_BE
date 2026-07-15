using Domain.Common;

namespace Domain.Entities
{
    // Vi tri dat chu ky truc quan (visual signature) tren file PDF; moi FileItem giu 1 vi tri hien hanh.
    public class FileSignaturePosition : IEntity
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

        public FileItem FileItem { get; set; } = null!;
    }
}
