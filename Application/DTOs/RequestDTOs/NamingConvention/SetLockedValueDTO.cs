using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.NamingConvention
{
    // Khóa 1 field vào đúng 1 value: backend luôn tự chèn value này khi sinh tên file.
    public class SetLockedValueDTO
    {
        [Required]
        public Guid ValueId { get; set; }
    }
}
