using System.ComponentModel.DataAnnotations;
using Domain.Enum.Project;

namespace Application.DTOs.RequestDTOs.Project
{
    public class UpdateParticipantStatusDTO
    {
        [Required]
        public ProjectParticipantStatus Status { get; set; }
    }
}
