using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs.RequestDTOs.Permission
{
    public class GetFolderPermissionOfParticipantDTO
    {
        [Required]
        public Guid FolderId { get; set; }

        [Required]
        public Guid ParticipantId { get; set; }
    }
}
