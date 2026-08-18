namespace Application.DTOs.ResponseDTOs.Approval
{
    public class ApprovalRequestPageDTO
    {
        public List<ApprovalRequestResponseDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
