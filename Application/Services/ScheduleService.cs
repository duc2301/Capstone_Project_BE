using Application.DTOs.RequestDTOs.Schedule;
using Application.DTOs.ResponseDTOs.Schedule;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ScheduleService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ScheduleResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ScheduleResponseDTO>>(
                await _unitOfWork.Repository<Schedule>().GetAllAsync());

        public async Task<ScheduleResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Schedule>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<ScheduleResponseDTO>(entity);
        }

        public async Task<ScheduleResponseDTO> CreateAsync(CreateScheduleDTO dto)
        {
            var entity = _mapper.Map<Schedule>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Schedule>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ScheduleResponseDTO>(entity);
        }

        public async Task<ScheduleResponseDTO> UpdateAsync(Guid id, UpdateScheduleDTO dto)
        {
            var entity = await _unitOfWork.Repository<Schedule>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Schedule with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Schedule>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ScheduleResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Schedule>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Schedule with ID {id} not found.", 404);
            _unitOfWork.Repository<Schedule>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
