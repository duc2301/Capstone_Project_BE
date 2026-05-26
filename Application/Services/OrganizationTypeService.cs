using Application.DTOs.RequestDTOs.OrganizationType;
using Application.DTOs.ResponseDTOs.OrganizationType;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class OrganizationTypeService : IOrganizationTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public OrganizationTypeService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<OrganizationTypeResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<OrganizationTypeResponseDTO>>(
                await _unitOfWork.Repository<OrganizationType>().GetAllAsync());

        public async Task<OrganizationTypeResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<OrganizationType>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<OrganizationTypeResponseDTO>(entity);
        }

        public async Task<OrganizationTypeResponseDTO> CreateAsync(CreateOrganizationTypeDTO dto)
        {
            var entity = _mapper.Map<OrganizationType>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<OrganizationType>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<OrganizationTypeResponseDTO>(entity);
        }

        public async Task<OrganizationTypeResponseDTO> UpdateAsync(Guid id, UpdateOrganizationTypeDTO dto)
        {
            var entity = await _unitOfWork.Repository<OrganizationType>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"OrganizationType with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<OrganizationType>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<OrganizationTypeResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<OrganizationType>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"OrganizationType with ID {id} not found.", 404);
            _unitOfWork.Repository<OrganizationType>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
