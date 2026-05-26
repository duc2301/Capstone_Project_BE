using Application.DTOs.RequestDTOs.Schedule;
using Application.DTOs.ResponseDTOs.Schedule;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IScheduleService
        : IGenericService<Schedule, CreateScheduleDTO, UpdateScheduleDTO, ScheduleResponseDTO>
    {
    }
}
