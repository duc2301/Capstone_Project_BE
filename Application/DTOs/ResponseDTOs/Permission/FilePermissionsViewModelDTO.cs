using Application.DTOs.RequestDTOs.Permission;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Permission
{
    public class FilePermissionsViewModelDTO
    {
        public List<ParticipantItems> AvailableGroups { get; set; } = new();     // Left panel
        public List<GroupFilePermissionResponseDTO> SelectedPermissions { get; set; } = new(); // Right panel
    }

    public class ParticipantItems
    {
        public Guid ProjectParticipantId { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public Guid? OrganizationId { get; set; }
        public string? OrganizationName { get; set; } 
    }
}
