using Application.DTOs.RequestDTOs.Schedule;
using Application.DTOs.ResponseDTOs.Schedule;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/schedules")]
    public class SchedulesController
        : BaseCrudController<Schedule, CreateScheduleDTO, UpdateScheduleDTO, ScheduleResponseDTO>
    {
        public SchedulesController(
            IGenericService<Schedule, CreateScheduleDTO, UpdateScheduleDTO, ScheduleResponseDTO> service)
            : base(service) { }
    }
}
