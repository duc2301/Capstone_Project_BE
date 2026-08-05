namespace Application.DTOs.ResponseDTOs.Profile
{
    // Profile của user hiện tại — đọc bằng JWT, không nhận id từ ngoài.
    public class ProfileResponseDTO : IResponseDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Role { get; set; }       // system role (Admin/User)
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string? AvatarUrl { get; set; }

        public bool IsEmailVerified { get; set; }

        public Guid? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }

        // Quan hệ user trong CDE — nhóm + vai trò trong nhóm
        public IList<ProfileGroupDTO> Groups { get; set; } = new List<ProfileGroupDTO>();
    }

    public class ProfileGroupDTO
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = null!;
        public string Role { get; set; } = null!;   // Leader/Member trong group
        public DateTime? JoinedAt { get; set; }

        public string? OrganizationName { get; set; }

        public IList<ProfileGroupProjectDTO> Projects { get; set; } = new List<ProfileGroupProjectDTO>();
    }

    public class ProfileGroupProjectDTO
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
    }
}
