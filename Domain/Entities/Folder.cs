using Domain.Enum.Cde;

using Domain.Common;

namespace Domain.Entities
{
    // Thư mục CDE: cây tự tham chiếu, gắn 1 khu vực CDE
    public class Folder : IEntity, IAuditable
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ParentFolderId { get; set; }
        public string Name { get; set; } = null!;
        public CdeArea Area { get; set; }
        public bool IsTemplate { get; set; }
        public Guid? CreatedByAccountId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public Folder? ParentFolder { get; set; }
        public ICollection<Folder> ChildFolders { get; set; } = new List<Folder>();
        public ICollection<FileItem> FileItems { get; set; } = new List<FileItem>();
        public ICollection<FolderPermission> Permissions { get; set; } = new List<FolderPermission>();
    }
}
