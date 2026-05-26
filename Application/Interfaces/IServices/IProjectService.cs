using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IProjectService
        : IGenericService<Project, CreateProjectDTO, UpdateProjectDTO, ProjectResponseDTO>
    {
    }
}
