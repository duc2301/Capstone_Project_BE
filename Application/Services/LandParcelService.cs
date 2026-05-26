using Application.DTOs.RequestDTOs.LandParcel;
using Application.DTOs.ResponseDTOs.LandParcel;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class LandParcelService : ILandParcelService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public LandParcelService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<LandParcelResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<LandParcelResponseDTO>>(
                await _unitOfWork.Repository<LandParcel>().GetAllAsync());

        public async Task<LandParcelResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<LandParcel>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<LandParcelResponseDTO>(entity);
        }

        public async Task<LandParcelResponseDTO> CreateAsync(CreateLandParcelDTO dto)
        {
            var entity = _mapper.Map<LandParcel>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<LandParcel>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<LandParcelResponseDTO>(entity);
        }

        public async Task<LandParcelResponseDTO> UpdateAsync(Guid id, UpdateLandParcelDTO dto)
        {
            var entity = await _unitOfWork.Repository<LandParcel>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"LandParcel with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<LandParcel>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<LandParcelResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<LandParcel>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"LandParcel with ID {id} not found.", 404);
            _unitOfWork.Repository<LandParcel>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
