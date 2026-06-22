using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Auth
{
    public class GoogleLoginDTO
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }
}
