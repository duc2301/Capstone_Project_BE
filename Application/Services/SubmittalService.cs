using Application.DTOs.RequestDTOs.Submittal;
using Application.DTOs.ResponseDTOs.Submittal;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class SubmittalService : ISubmittalService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public SubmittalService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<SubmittalResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<SubmittalResponseDTO>>(
                await _unitOfWork.Repository<Submittal>().GetAllAsync());

        public async Task<SubmittalResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Submittal>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<SubmittalResponseDTO>(entity);
        }

        public async Task<SubmittalResponseDTO> CreateAsync(CreateSubmittalDTO dto)
        {
            var entity = _mapper.Map<Submittal>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Submittal>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<SubmittalResponseDTO>(entity);
        }

        public async Task<SubmittalResponseDTO> UpdateAsync(Guid id, UpdateSubmittalDTO dto)
        {
            var entity = await _unitOfWork.Repository<Submittal>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Submittal with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Submittal>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<SubmittalResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Submittal>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Submittal with ID {id} not found.", 404);
            _unitOfWork.Repository<Submittal>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
