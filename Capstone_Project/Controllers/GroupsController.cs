using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/groups")]
    public class GroupsController
        : BaseCrudController<Group, CreateGroupDTO, UpdateGroupDTO, GroupResponseDTO>
    {
        public GroupsController(
            IGenericService<Group, CreateGroupDTO, UpdateGroupDTO, GroupResponseDTO> service)
            : base(service) { }
    }
}
