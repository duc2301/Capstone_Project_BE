namespace Application.DTOs.ResponseDTOs.NamingConvention
{
    // Kết quả sinh tên file từ naming convention.
    // HasNamingConvention = false: folder không có convention -> upload dùng tên gốc như cũ.
    public class FileNameGenerationResultDTO
    {
        public bool HasNamingConvention { get; set; }
        public string FileName { get; set; } = string.Empty;              // HTM-ARC-L01-DR-A-0001.pdf
        public string FileNameWithoutExtension { get; set; } = string.Empty; // HTM-ARC-L01-DR-A-0001
        public List<ResolvedNamingSegmentDTO> Segments { get; set; } = new();
    }

    // 1 segment đã resolve: dùng để lưu FileNamingMetadata (audit vì sao file có tên này).
    public class ResolvedNamingSegmentDTO
    {
        public Guid FieldId { get; set; }
        public string FieldCode { get; set; } = string.Empty;
        public Guid ValueId { get; set; }
        public string ValueCode { get; set; } = string.Empty;
        public string ValueDisplayName { get; set; } = string.Empty;
    }
}
