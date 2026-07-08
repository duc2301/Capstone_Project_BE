using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs.RequestDTOs.Permission
{
    public class GetFilePermissionOfParticipantDTO
    {
        [Required]
        public Guid FileItemId { get; set; }

        [Required]
        public Guid ParticipantId { get; set; }
    }
}
