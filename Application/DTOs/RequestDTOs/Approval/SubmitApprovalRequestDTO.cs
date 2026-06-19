namespace Application.DTOs.RequestDTOs.Approval
{
    /// <summary>
    /// Request member gui khi submit file de duyet.
    /// </summary>
    public class SubmitApprovalRequestDTO
    {
        /// <summary>True neu file can Leader ky so VNPT SmartCA truoc khi approve.</summary>
        public bool RequiresSignature { get; set; }
    }
}
