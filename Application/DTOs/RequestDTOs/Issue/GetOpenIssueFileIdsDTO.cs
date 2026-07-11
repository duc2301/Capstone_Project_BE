namespace Application.DTOs.RequestDTOs.Issue
{
    public class GetOpenIssueFileIdsDTO
    {
        public List<Guid> FileItemIds { get; set; } = new();
    }
}
