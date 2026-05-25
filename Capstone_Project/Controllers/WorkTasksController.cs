using Application.DTOs.RequestDTOs.WorkTask;
using Application.DTOs.ResponseDTOs.WorkTask;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/work-tasks")]
    public class WorkTasksController
        : BaseCrudController<WorkTask, CreateWorkTaskDTO, UpdateWorkTaskDTO, WorkTaskResponseDTO>
    {
        public WorkTasksController(
            IGenericService<WorkTask, CreateWorkTaskDTO, UpdateWorkTaskDTO, WorkTaskResponseDTO> service)
            : base(service) { }
    }
}
