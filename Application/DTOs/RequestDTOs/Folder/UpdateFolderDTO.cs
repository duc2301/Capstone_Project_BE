using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Folder
{
    public class UpdateFolderDTO
    {
        [Required]
        [StringLength(250, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;
    }
}
