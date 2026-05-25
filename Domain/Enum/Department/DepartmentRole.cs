namespace Domain.Enum.Department
{
    // Role của 1 tài khoản TRONG MỖI phòng ban (theo từng Employee, không toàn cục)
    public enum DepartmentRole
    {
        Member,           // Nhân viên - up tài liệu
        DepartmentHead   // Trưởng ban - duyệt nội bộ trước khi chia sẻ cho bên khác (cổng WIP -> Shared)
    }
}
