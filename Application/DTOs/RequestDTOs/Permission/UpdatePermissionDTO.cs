using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.RequestDTOs.Permission
{
    public class UpdatePermissionDTO
    {
        public Guid ProjectParticipantId { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDownload { get; set; }
        public bool CanVerify { get; set; }
        public bool CanApprove { get; set; }
    }
}
