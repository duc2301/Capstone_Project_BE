using Domain.Enum.Submittal;

namespace Domain.Entities
{
    // 1 bước trong workflow phiếu: Trình nộp / Thẩm tra / Duyệt
    public class SubmittalStep
    {
        public Guid Id { get; set; }
        public Guid SubmittalId { get; set; }
        public int StepOrder { get; set; }
        public SubmittalStepType StepType { get; set; }
        public Guid? AssignedOrganizationId { get; set; }
        public Guid? AssignedAccountId { get; set; }
        public SubmittalStepAction Action { get; set; }
        public string? Comment { get; set; }
        public Guid? ActedByAccountId { get; set; }
        public DateTime? ActedAt { get; set; }

        public Submittal Submittal { get; set; } = null!;
    }
}
