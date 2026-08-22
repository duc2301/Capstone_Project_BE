using Domain.Enum.Permission;
using System;
using System.Collections.Generic;

namespace Application.DTOs.ResponseDTOs.Permission
{
    // Dữ liệu cho hộp thoại "Phân quyền thành viên" (kiểu blacklist).
    // Một danh sách PHẲNG mọi thành viên đang có quyền trên tài nguyên NHỜ nhóm của họ, kèm mức quyền
    // kế thừa từ nhóm và trạng thái bị chặn (blacklist). Leader chỉ có thể CHẶN (thu hồi read/write)
    // từng thành viên; không cấp quyền cho người ngoài từ đây.
    public class MemberPermissionsViewModelDTO
    {
        public List<MemberPermissionItemDTO> Members { get; set; } = new();
    }

    public class MemberPermissionItemDTO
    {
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // Nhóm nào đang cấp quyền cho thành viên này trên tài nguyên (để hiển thị nguồn quyền).
        public List<string> Groups { get; set; } = new();

        // Quyền kế thừa từ nhóm (nguồn = ma trận/phân quyền nhóm). Roster chỉ gồm người có View, nên
        // InheritedCanView luôn true; InheritedCanEdit = true nếu bất kỳ nhóm nào cho Sửa.
        public bool InheritedCanView { get; set; }
        public bool InheritedCanEdit { get; set; }

        // Mức override riêng của tài khoản trên CHÍNH tài nguyên này (điều khiển selector trên UI):
        //   "None"    = không có override -> kế thừa quyền nhóm
        //   "View"    = cấp Xem riêng     (override CanView=true, CanEdit=false)
        //   "Edit"    = cấp Sửa riêng     (override CanView=true, CanEdit=true)
        //   "Blocked" = chặn              (override CanView=false)
        public string OverrideLevel { get; set; } = "None";

        // Tiện ích tương thích ngược: = (OverrideLevel == "Blocked").
        public bool IsBlacklisted { get; set; }
    }

    // Kết quả trả về của đường lưu (giữ nguyên): các dòng override đã đụng tới sau khi lưu.
    public class UserPermissionResponseDTO
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public PermissionStatus Status { get; set; }
    }

    // ===== Hợp đồng nội bộ repo -> service để dựng roster =====

    // Một dòng cấp quyền nhóm (file hoặc folder) đã resolve, kèm tên nhóm.
    public class GroupGrantDTO
    {
        public Guid ParticipantId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
    }

    // Một thành viên đang hoạt động của một ProjectParticipant (để bung nhóm -> người).
    public class MemberOfParticipantDTO
    {
        public Guid ParticipantId { get; set; }
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
