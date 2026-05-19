using Application.DTOs.RequestDTOs.ProjectModel;
using Application.DTOs.ResponseDTOs.ProjectModel;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/project-models")]
    public class ProjectModelsController
        : BaseCrudController<ProjectModel, CreateProjectModelDTO, UpdateProjectModelDTO, ProjectModelResponseDTO>
    {
        public ProjectModelsController(
            IGenericService<ProjectModel, CreateProjectModelDTO, UpdateProjectModelDTO, ProjectModelResponseDTO> service)
            : base(service) { }
    }
}
