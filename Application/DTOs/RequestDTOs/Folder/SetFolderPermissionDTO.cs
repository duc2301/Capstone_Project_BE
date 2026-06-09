using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Folder
{
    // PM set/replace 1 dòng ACL override cho folder, nhắm vào đúng 1 đối tượng:
    // Group HOẶC Organization (chỉ điền 1 trong 2). Upsert theo (FolderId, GroupId|OrganizationId).
    public class SetFolderPermissionDTO
    {
        public Guid? GroupId { get; set; }
        public Guid? OrganizationId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDownload { get; set; }
        public bool CanVerify { get; set; }
        public bool CanApprove { get; set; }

        public bool InheritFromParent { get; set; } = true;
    }
}
