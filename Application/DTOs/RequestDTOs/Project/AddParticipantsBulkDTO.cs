using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Project
{
    // Add nhiều bên tham gia (department/team/organization) cho project trong 1 transaction.
    public class AddParticipantsBulkDTO
    {
        [Required]
        [MinLength(1)]
        public List<AddParticipantDTO> Participants { get; set; } = new();
    }
}
