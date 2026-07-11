using System.ComponentModel.DataAnnotations;
using Domain.Enum.FileNaming;

namespace Application.DTOs.RequestDTOs.NamingConvention
{
    public class UpdateNamingConventionDTO
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(1)]
        public string? Delimiter { get; set; }

        public bool? IsActive { get; set; }
    }

    public class UpdateNamingFieldDTO
    {
        [StringLength(50)]
        public string? Code { get; set; }

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public int? OrderIndex { get; set; }

        public bool? IsRequired { get; set; }

        public int? MinLength { get; set; }

        public int? MaxLength { get; set; }

        public NamingFieldType? FieldType { get; set; }
    }

    public class UpdateNamingFieldValueDTO
    {
        [StringLength(50)]
        public string? Code { get; set; }

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public int? OrderIndex { get; set; }

        public bool? IsActive { get; set; }
    }
}
