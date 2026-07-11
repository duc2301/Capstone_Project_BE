namespace Application.DTOs.ResponseDTOs.NamingConvention
{
    // Payload gọn cho dialog upload: chỉ trả những gì UI cần để render dropdown.
    // Field bị khóa: trả lockedValue, KHÔNG trả values (FE không render dropdown cho nó).
    public class FolderNamingConventionResponseDTO
    {
        public bool HasNamingConvention { get; set; }
        public Guid? NamingConventionId { get; set; }
        public string? Delimiter { get; set; }
        public List<UploadNamingFieldDTO>? Fields { get; set; }
    }

    public class UploadNamingFieldDTO
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public bool Required { get; set; }
        public bool Locked { get; set; }
        public UploadNamingValueDTO? LockedValue { get; set; }
        public List<UploadNamingValueDTO>? Values { get; set; }
    }

    public class UploadNamingValueDTO
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
