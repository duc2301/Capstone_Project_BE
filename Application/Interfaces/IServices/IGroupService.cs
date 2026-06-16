using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Domain.Entities;
using Domain.Enum.Group;

namespace Application.Interfaces.IServices
{
    public interface IGroupService
        : IGenericService<Group, CreateGroupDTO, UpdateGroupDTO, GroupResponseDTO>
    {
        // Đổi vai trò thành viên (Role=Leader => chuyển trưởng nhóm).
        Task<GroupResponseDTO> ChangeMemberRoleAsync(Guid groupId, Guid accountId, GroupMemberRole newRole);

        Task<GroupResponseDTO> ChangeMemberStatusAsync(Guid groupId, Guid accountId, GroupMemberStatus newStatus);
    }
}
