namespace Application.DTOs.RequestDTOs.FileItem
{
    public class CreateZoneReturnRequestDTO
    {
        public string Reason { get; set; } = string.Empty;

        /// <summary>Neu request nay duoc tao tu 1 Issue, truyen id de lien ket (khong bat buoc).</summary>
        public Guid? IssueId { get; set; }
    }
}
