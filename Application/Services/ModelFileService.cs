using Application.DTOs.RequestDTOs.ModelFile;
using Application.DTOs.ResponseDTOs.ModelFile;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class ModelFileService : IModelFileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ModelFileService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ModelFileResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ModelFileResponseDTO>>(
                await _unitOfWork.Repository<ModelFile>().GetAllAsync());

        public async Task<ModelFileResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ModelFile>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<ModelFileResponseDTO>(entity);
        }

        public async Task<ModelFileResponseDTO> CreateAsync(CreateModelFileDTO dto)
        {
            var entity = _mapper.Map<ModelFile>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<ModelFile>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ModelFileResponseDTO>(entity);
        }

        public async Task<ModelFileResponseDTO> UpdateAsync(Guid id, UpdateModelFileDTO dto)
        {
            var entity = await _unitOfWork.Repository<ModelFile>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ModelFile with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<ModelFile>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ModelFileResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ModelFile>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ModelFile with ID {id} not found.", 404);
            _unitOfWork.Repository<ModelFile>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
