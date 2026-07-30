namespace Application.DTOs.ResponseDTOs.Account
{
    public class AccountResponseDTO
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Role { get; set; }
        public string? Status { get; set; }

        public Guid? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }

        public string? AvatarUrl { get; set; }

        public List<AccountManagedProjectDTO> ManagedProjects { get; set; } = new();

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
