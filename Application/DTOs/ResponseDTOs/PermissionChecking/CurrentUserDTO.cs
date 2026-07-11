namespace Application.DTOs.ResponseDTOs.PermissionChecking
{
    public class CurrentUserDTO
    {
        public Guid AccountId { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
    }
}
