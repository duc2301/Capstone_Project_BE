using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Folder
{
    // Team Leader tạo thư mục con để tổ chức file.
    // Area/OwnerGroup/OwnerOrganization được kế thừa từ folder cha (không nhận từ client).
    public class CreateSubFolderDTO
    {
        [Required]
        public Guid ParentFolderId { get; set; }

        [Required]
        [StringLength(250)]
        public string Name { get; set; } = null!;
    }
}
