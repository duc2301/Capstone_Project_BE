namespace Application.DTOs.RequestDTOs.Approval
{
    /// <summary>
    /// Request member gui khi submit file de duyet.
    /// </summary>
    public class SubmitApprovalRequestDTO
    {
        /// <summary>Target zone. Neu null thi BE tu lay zone tiep theo cua file.</summary>
        public string? TargetZone { get; set; }

        /// <summary>True neu approval request nay can ky so VNPT SmartCA truoc khi approve.</summary>
        public bool RequiresSignature { get; set; }

        /// <summary>Danh sach account can ky. Dung khi leader chi dinh nguoi ky cu the.</summary>
        public List<Guid> SignerAccountIds { get; set; } = new();

        /// <summary>Danh sach group can ky. Neu empty va RequiresSignature=true, BE fallback ve team phu trach file.</summary>
        public List<Guid> SignerGroupIds { get; set; } = new();
    }
}
