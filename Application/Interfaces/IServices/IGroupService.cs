using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Domain.Enum.Group;

namespace Application.Interfaces.IServices
{
    public interface IGroupService
    {
        Task<IEnumerable<GroupResponseDTO>> GetAllAsync();
        Task<GroupResponseDTO?> GetByIdAsync(Guid id);
        Task<GroupResponseDTO> CreateAsync(CreateGroupDTO dto);
        // actor/actorRole do controller lấy từ JWT (kiểm tra Admin/PM trước khi sửa/xóa).
        Task<GroupResponseDTO> UpdateAsync(Guid id, UpdateGroupDTO dto, Guid actorId, string? actorRole);
        Task DeleteAsync(Guid id, Guid actorId, string? actorRole);

        // Đổi vai trò thành viên (Role=Leader => chuyển trưởng nhóm).
        Task<GroupResponseDTO> ChangeMemberRoleAsync(Guid groupId, Guid accountId, GroupMemberRole newRole, Guid actorId, string? actorRole);

        Task<GroupResponseDTO> ChangeMemberStatusAsync(Guid groupId, Guid accountId, GroupMemberStatus newStatus, Guid actorId, string? actorRole, string? actorName);
    }
}
