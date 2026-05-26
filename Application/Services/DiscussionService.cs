using Application.DTOs.RequestDTOs.Discussion;
using Application.DTOs.ResponseDTOs.Discussion;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class DiscussionService : IDiscussionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public DiscussionService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<DiscussionResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<DiscussionResponseDTO>>(
                await _unitOfWork.Repository<Discussion>().GetAllAsync());

        public async Task<DiscussionResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Discussion>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task<DiscussionResponseDTO> CreateAsync(CreateDiscussionDTO dto)
        {
            var entity = _mapper.Map<Discussion>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Discussion>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task<DiscussionResponseDTO> UpdateAsync(Guid id, UpdateDiscussionDTO dto)
        {
            var entity = await _unitOfWork.Repository<Discussion>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Discussion with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Discussion>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<DiscussionResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Discussion>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Discussion with ID {id} not found.", 404);
            _unitOfWork.Repository<Discussion>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
