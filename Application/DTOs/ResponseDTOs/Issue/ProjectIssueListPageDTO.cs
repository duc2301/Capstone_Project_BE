namespace Application.DTOs.ResponseDTOs.Issue
{
    public class ProjectIssueListPageDTO
    {
        public List<ProjectIssueListItemDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
