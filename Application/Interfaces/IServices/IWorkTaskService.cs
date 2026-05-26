using Application.DTOs.RequestDTOs.WorkTask;
using Application.DTOs.ResponseDTOs.WorkTask;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IWorkTaskService
        : IGenericService<WorkTask, CreateWorkTaskDTO, UpdateWorkTaskDTO, WorkTaskResponseDTO>
    {
    }
}
