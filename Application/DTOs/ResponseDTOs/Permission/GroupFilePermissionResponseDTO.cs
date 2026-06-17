using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Permission
{
    public class GroupFilePermissionResponseDTO
    {
        public Guid ProjectParticipantId { get; set; }

        public string GroupParticipantName { get; set; } = null!;

        public bool CanView { get; set; }

        public bool CanEdit { get; set; }

        public bool CanUpdate { get; set; }

        public bool CanDownload { get; set; }

        public bool CanVerify { get; set; }

        public bool CanApprove { get; set; }

        public bool InheritFromParent { get; set; }
    }
}
