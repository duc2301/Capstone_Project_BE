using Domain.Enum.FileNaming;

namespace Application.DTOs.ResponseDTOs.NamingConvention
{
    // Chi tiết đầy đủ cho trang cấu hình của admin.
    public class NamingConventionResponseDTO
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Delimiter { get; set; } = "-";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<NamingFieldResponseDTO> Fields { get; set; } = new();
        public List<AssignedFolderResponseDTO> AssignedFolders { get; set; } = new();
    }

    public class NamingFieldResponseDTO
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public bool IsRequired { get; set; }
        public bool IsLocked { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public NamingFieldType FieldType { get; set; }
        public List<NamingFieldValueResponseDTO> AllowedValues { get; set; } = new();
        public NamingFieldValueResponseDTO? LockedValue { get; set; }
    }

    public class NamingFieldValueResponseDTO
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public bool IsActive { get; set; }
    }

    public class AssignedFolderResponseDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
