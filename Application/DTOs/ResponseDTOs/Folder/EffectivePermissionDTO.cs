namespace Application.DTOs.ResponseDTOs.Folder
{
    // Quyền hiệu lực của 1 account trên 1 folder, sau khi gộp:
    // bypass (Admin/PM) -> baseline theo CdeArea + quyền sở hữu -> các dòng override.
    public class EffectivePermissionDTO
    {
        public Guid FolderId { get; set; }
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDownload { get; set; }
        public bool CanVerify { get; set; }
        public bool CanApprove { get; set; }
    }
}
