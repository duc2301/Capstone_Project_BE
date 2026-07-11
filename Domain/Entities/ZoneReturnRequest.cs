using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Domain.Entities
{
    public class ZoneReturnRequest
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        // Neu request nay duoc tao tu 1 Issue (path "Can sua file" cua issue workflow) -> luu lai de
        // issue detail biet dang cho duyet o dau. Loose FK (khong navigation/constraint), giong idiom
        // MarkupSet.IssueId / Discussion.ScopeId da dung trong codebase.
        public Guid? IssueId { get; set; }
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
