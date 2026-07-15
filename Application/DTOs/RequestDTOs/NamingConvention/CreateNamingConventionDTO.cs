using System.ComponentModel.DataAnnotations;
using Domain.Enum.FileNaming;

namespace Application.DTOs.RequestDTOs.NamingConvention
{
    // Tạo naming convention mức dự án, kèm sẵn danh sách field + allowed values (nested).
    public class CreateNamingConventionDTO
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Ký tự phân tách giữa các segment: - _ .
        [Required, StringLength(1)]
        public string Delimiter { get; set; } = "-";

        public List<CreateNamingFieldDTO> Fields { get; set; } = new();
    }

    public class CreateNamingFieldDTO
    {
        [Required, StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int OrderIndex { get; set; }

        public bool IsRequired { get; set; } = true;

        public bool IsLocked { get; set; }

        public int? MinLength { get; set; }

        public int? MaxLength { get; set; }

        public NamingFieldType FieldType { get; set; } = NamingFieldType.Custom;

        public List<CreateNamingFieldValueDTO> AllowedValues { get; set; } = new();

        // Chỉ dùng khi IsLocked = true: Code của value (trong AllowedValues) được khóa cứng.
        [StringLength(50)]
        public string? LockedValueCode { get; set; }
    }

    public class CreateNamingFieldValueDTO
    {
        [Required, StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int OrderIndex { get; set; }
    }
}
