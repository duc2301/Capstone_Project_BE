using Application.DTOs.ResponseDTOs.Issue;

namespace Application.Interfaces.IServices
{
    // Đẩy realtime state của 1 issue (không phải bell notification) tới mọi client đang mở
    // panel issue của file đó (room theo fileItemId), để FE tự vá vào danh sách, không cần refetch.
    // Implementation thật nằm ở Capstone_Project/SignalR/SignalRIssueBroadcaster.cs
    public interface IIssueBroadcaster
    {
        Task IssueCreatedAsync(Guid fileItemId, IssueResponseDTO issue);
        Task IssueUpdatedAsync(Guid fileItemId, IssueResponseDTO issue);
    }
}
