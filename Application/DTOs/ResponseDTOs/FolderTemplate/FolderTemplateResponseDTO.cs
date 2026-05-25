namespace Application.DTOs.ResponseDTOs.FolderTemplate
{
    public class FolderTemplateResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string StructureJson { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
    }
}
