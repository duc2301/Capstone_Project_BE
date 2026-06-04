namespace Application.DTOs.ResponseDTOs.Viewer
{
    public class ViewerTokenResponseDTO
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
