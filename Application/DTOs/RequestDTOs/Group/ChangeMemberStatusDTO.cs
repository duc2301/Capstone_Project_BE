using System.ComponentModel.DataAnnotations;
using Domain.Enum.Group;

namespace Application.DTOs.RequestDTOs.Group
{
    public class ChangeMemberStatusDTO
    {
        [Required]
        public GroupMemberStatus Status { get; set; }
    }
}
