using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.NamingConvention
{
    // Gán 1 naming convention cho nhiều folder (cùng dự án).
    public class AssignFoldersDTO
    {
        [Required, MinLength(1)]
        public List<Guid> FolderIds { get; set; } = new();

        // true: gán luôn cho toàn bộ cây thư mục con của từng folder.
        public bool ApplyToSubfolders { get; set; }
    }
}
