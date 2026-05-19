namespace Application.DTOs.ResponseDTOs.Auth
{
    public class AuthResponseDTO
    {
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;

        public string AccessToken { get; set; } = null!;
        public DateTime AccessTokenExpiresAt { get; set; }

        public string RefreshToken { get; set; } = null!;
        public DateTime RefreshTokenExpiresAt { get; set; }
    }
}
