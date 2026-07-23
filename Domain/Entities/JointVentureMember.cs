using Domain.Common;

namespace Domain.Entities
{
    public class JointVentureMember : IEntity
    {
        public Guid Id { get; set; }
        public Guid JointVentureId { get; set; }
        public Guid MemberOrganizationId { get; set; }

        public Organization JointVenture { get; set; } = null!;
        public Organization MemberOrganization { get; set; } = null!;
    }
}
