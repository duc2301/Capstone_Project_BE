using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Domain.Entities
{
    public class ZoneReturnRequest
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public CdeArea FromZone { get; set; }
        public CdeArea TargetZone { get; set; }
        public Guid RequestedBy { get; set; }
        public Guid? ApprovedBy { get; set; }
        public ZoneReturnRequestStatus Status { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DecidedAt { get; set; }

        public FileItem FileItem { get; set; } = null!;
        public Account Requester { get; set; } = null!;
        public Account? Approver { get; set; }
    }
}
