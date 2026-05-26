using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IGroupService
        : IGenericService<Group, CreateGroupDTO, UpdateGroupDTO, GroupResponseDTO>
    {
    }
}
