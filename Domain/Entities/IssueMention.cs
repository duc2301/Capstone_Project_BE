namespace Domain.Entities
{
    // "Táp" 1 bên vào RFI/Issue -> sinh thông báo có liên kết tới phiếu
    public class IssueMention
    {
        public Guid Id { get; set; }
        public Guid IssueId { get; set; }
        public Guid MentionedAccountId { get; set; }

        public Issue Issue { get; set; } = null!;
    }
}
