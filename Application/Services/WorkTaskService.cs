using Application.DTOs.RequestDTOs.WorkTask;
using Application.DTOs.ResponseDTOs.WorkTask;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class WorkTaskService : IWorkTaskService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public WorkTaskService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<WorkTaskResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<WorkTaskResponseDTO>>(
                await _unitOfWork.Repository<WorkTask>().GetAllAsync());

        public async Task<WorkTaskResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<WorkTask>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<WorkTaskResponseDTO>(entity);
        }

        public async Task<WorkTaskResponseDTO> CreateAsync(CreateWorkTaskDTO dto)
        {
            var entity = _mapper.Map<WorkTask>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<WorkTask>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<WorkTaskResponseDTO>(entity);
        }

        public async Task<WorkTaskResponseDTO> UpdateAsync(Guid id, UpdateWorkTaskDTO dto)
        {
            var entity = await _unitOfWork.Repository<WorkTask>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"WorkTask with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<WorkTask>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<WorkTaskResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<WorkTask>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"WorkTask with ID {id} not found.", 404);
            _unitOfWork.Repository<WorkTask>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
