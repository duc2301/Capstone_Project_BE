namespace Application.DTOs.ResponseDTOs.Issue
{
    public class IssueAttachmentResponseDTO
    {
        public Guid Id { get; set; }
        public string? Url { get; set; }
        public Guid? FileVersionId { get; set; }
    }
}
