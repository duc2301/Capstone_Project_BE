using Domain.Enum.Cde;
using Domain.Enum.Permission;

namespace Application.DTOs.ResponseDTOs.PermissionMatrix
{
    // Toàn bộ lưới quyền của 1 dự án mà người gọi được phép thấy.
    // Cột = nhóm tham gia; hàng = cây thư mục/file (đã lọc theo quyền View của người gọi).
    // Enum serialize ra SỐ (BE không dùng JsonStringEnumConverter) -> FE khai báo numeric union khớp.
    public class PermissionMatrixResponseDTO
    {
        public Guid ProjectId { get; set; }
        public List<MatrixColumnDTO> Columns { get; set; } = new();
        public List<MatrixRowDTO> Rows { get; set; } = new();
        public MatrixActorScopeDTO Actor { get; set; } = new();
    }

    // Một cột = một bên tham gia (ProjectParticipant = 1 nhóm trong dự án).
    public class MatrixColumnDTO
    {
        public Guid ProjectParticipantId { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
    }

    // Một hàng = một thư mục hoặc file. Root area (WIP/Shared/Published/Archived) chỉ để hiển thị
    // (Assignable=false). ParentRowId là cha hiển thị (tổ tiên gần nhất còn thấy được).
    public class MatrixRowDTO
    {
        public Guid TargetId { get; set; }
        public MatrixTargetType TargetType { get; set; }
        public Guid? ParentRowId { get; set; }
        public string Name { get; set; } = string.Empty;
        public CdeArea Area { get; set; }
        public bool IsRootArea { get; set; }
        public bool Assignable { get; set; }
        public List<MatrixCellDTO> Cells { get; set; } = new();
    }

    // Một ô = mức quyền hiện tại của (hàng, cột). IsInherited=true khi file suy quyền từ thư mục
    // (chưa có override riêng). Editable=false khi người gọi không được sửa ô này.
    public class MatrixCellDTO
    {
        public Guid ProjectParticipantId { get; set; }
        public PermissionLevel Level { get; set; }
        public bool IsInherited { get; set; }
        public bool Editable { get; set; }
    }

    // Phạm vi sửa của người gọi: full access (admin/PM/PA) hay leader (chỉ nhánh được giao).
    public class MatrixActorScopeDTO
    {
        public bool IsFullAccess { get; set; }
        public List<Guid> EditableFolderIds { get; set; } = new();
    }
}
