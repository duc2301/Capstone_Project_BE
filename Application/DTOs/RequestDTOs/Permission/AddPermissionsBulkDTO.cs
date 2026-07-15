using Application.DTOs.RequestDTOs.Project;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs.RequestDTOs.Permission
{
    public class AddPermissionsBulkDTO
    {
        public Guid Id { get; set; }

        [Required]
        [MinLength(1)]
        public List<AddPermissionDTO> GroupsPermission { get; set; } = new();

        public List<Guid> RemoveParticipantIds { get; set; } = new();
    }
}
