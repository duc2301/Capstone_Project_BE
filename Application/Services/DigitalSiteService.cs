using Application.DTOs.RequestDTOs.DigitalSite;
using Application.DTOs.ResponseDTOs.DigitalSite;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class DigitalSiteService : IDigitalSiteService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public DigitalSiteService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<DigitalSiteResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<DigitalSiteResponseDTO>>(
                await _unitOfWork.Repository<DigitalSite>().GetAllAsync());

        public async Task<DigitalSiteResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<DigitalSite>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<DigitalSiteResponseDTO>(entity);
        }

        public async Task<DigitalSiteResponseDTO> CreateAsync(CreateDigitalSiteDTO dto)
        {
            var entity = _mapper.Map<DigitalSite>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<DigitalSite>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<DigitalSiteResponseDTO>(entity);
        }

        public async Task<DigitalSiteResponseDTO> UpdateAsync(Guid id, UpdateDigitalSiteDTO dto)
        {
            var entity = await _unitOfWork.Repository<DigitalSite>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"DigitalSite with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<DigitalSite>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<DigitalSiteResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<DigitalSite>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"DigitalSite with ID {id} not found.", 404);
            _unitOfWork.Repository<DigitalSite>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
