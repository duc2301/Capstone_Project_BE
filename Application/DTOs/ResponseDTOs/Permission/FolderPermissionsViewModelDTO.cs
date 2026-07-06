using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Permission
{
    public class FolderPermissionsViewModelDTO
    {
        public List<ParticipantItems> AvailableGroups { get; set; } = new();     // Left panel
        public List<GroupFolderPermissionResponseDTO> SelectedPermissions { get; set; } = new(); // Right panel
    }
}
