namespace Application.DTOs.ResponseDTOs.PermissionChecking
{
    public class CurrentUserGroupDTO
    {
        public Guid GroupId { get; set; }
        public string Name { get; set; } = null!;
        public Guid? OrganizationId { get; set; }
    }
}
