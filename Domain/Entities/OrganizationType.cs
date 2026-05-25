using Domain.Common;

namespace Domain.Entities
{
    // Lookup table cho "loại bên tham gia" — Admin có thể thêm runtime.
    // Seed sẵn 8 loại theo ISO 19650 / TCVN 14177 trong OnModelCreating.
    public class OrganizationType : IEntity
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;        // mã ngắn duy nhất, ổn định cho code (vd "MainContractor")
        public string Name { get; set; } = null!;        // tên hiển thị
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Organization> Organizations { get; set; } = new List<Organization>();
    }
}
