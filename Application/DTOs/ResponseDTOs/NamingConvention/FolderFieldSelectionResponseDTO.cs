namespace Application.DTOs.ResponseDTOs.NamingConvention
{
    // Toàn bộ field của convention đang áp cho folder + cờ enabled.
    // Field bắt buộc / khóa: Enabled luôn = true, không tắt được. Field tùy chọn: bật/tắt theo folder.
    public class FolderFieldSelectionResponseDTO
    {
        public bool HasNamingConvention { get; set; }
        public Guid? NamingConventionId { get; set; }
        public List<FolderFieldOptionDTO>? Fields { get; set; }
    }

    public class FolderFieldOptionDTO
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public bool IsRequired { get; set; }
        public bool IsLocked { get; set; }
        public bool Enabled { get; set; }
    }
}
