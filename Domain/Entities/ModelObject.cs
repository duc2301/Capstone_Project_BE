namespace Domain.Entities
{
    // Đối tượng trong file IFC để gắn vào công tác. Màu thi công SUY RA từ
    // WorkTask liên kết qua WorkTaskModelLink — không lưu cache (quyết định c).
    public class ModelObject
    {
        public Guid Id { get; set; }
        public Guid ModelFileId { get; set; }
        public string ObjectGuid { get; set; } = null!;   // IFC GUID
        public string? Name { get; set; }

        public ModelFile ModelFile { get; set; } = null!;
    }
}
