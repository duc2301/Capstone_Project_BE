using Domain.Entities;

namespace Application.DTOs.ResponseDTOs.SmartCA
{
    /// <summary>Ngu canh da xac thuc cua 1 thao tac ky SmartCA: approval/file/folder/signer lien quan.</summary>
    // FileItem/Folder phai ghi day du namespace vi trung ten voi sibling namespace
    // Application.DTOs.ResponseDTOs.FileItem/Folder (cac folder DTO cung cap).
    public sealed record SigningContext(
        ApprovalRequest ApprovalRequest,
        Domain.Entities.FileItem FileItem,
        Domain.Entities.Folder Folder,
        ApprovalRequestSigner Signer);
}
