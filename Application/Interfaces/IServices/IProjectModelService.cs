using Application.DTOs.RequestDTOs.ProjectModel;
using Application.DTOs.ResponseDTOs.ProjectModel;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IProjectModelService
        : IGenericService<ProjectModel, CreateProjectModelDTO, UpdateProjectModelDTO, ProjectModelResponseDTO>
    {
    }
}
