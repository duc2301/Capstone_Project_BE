using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/projects")]
    public class ProjectsController
        : BaseCrudController<Project, CreateProjectDTO, UpdateProjectDTO, ProjectResponseDTO>
    {
        public ProjectsController(
            IGenericService<Project, CreateProjectDTO, UpdateProjectDTO, ProjectResponseDTO> service)
            : base(service) { }
    }
}
