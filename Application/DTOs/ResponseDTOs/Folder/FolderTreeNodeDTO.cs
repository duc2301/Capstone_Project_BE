using Domain.Enum.Cde;

namespace Application.DTOs.ResponseDTOs.Folder
{
    // 1 nút trong cây thư mục CDE đã lọc theo quyền của người gọi.
    // Permission = quyền hiệu lực của chính người gọi trên nút này.
    public class FolderTreeNodeDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ParentFolderId { get; set; }
        public string Name { get; set; } = null!;
        public CdeArea Area { get; set; }
        public Guid? OwnerOrganizationId { get; set; }
        public Guid? OwnerGroupId { get; set; }

        public EffectivePermissionDTO Permission { get; set; } = new();
        public List<FolderTreeNodeDTO> Children { get; set; } = new();
    }
}
