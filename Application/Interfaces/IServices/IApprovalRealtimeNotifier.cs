using Application.DTOs.ResponseDTOs.Approval;

namespace Application.Interfaces.IServices
{
    // Đẩy realtime state của 1 approval request (không phải bell notification) qua SignalR
    // để FE tự vá vào danh sách approval đang mở, không cần refetch/nháy trang.
    // Implementation thật nằm ở Capstone_Project/SignalR/SignalRApprovalNotifier.cs
    public interface IApprovalRealtimeNotifier
    {
        Task ApprovalChangedAsync(Guid accountId, ApprovalRequestResponseDTO approval);
    }
}
