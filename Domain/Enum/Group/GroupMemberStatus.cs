using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Enum.Group
{
    public enum GroupMemberStatus
    {
        Pending,    // Đang chờ vào nhóm
        Active,     // Đã được duyệt, đang là member
        Left        // Đã rời nhóm (do member tự rời hoặc bị leader kick)
    }
}
