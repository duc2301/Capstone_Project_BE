using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.ResponseDTOs.Organization;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public OrganizationService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<OrganizationResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<OrganizationResponseDTO>>(
                await _unitOfWork.Repository<Organization>().GetAllAsync());

        public async Task<OrganizationResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Organization>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<OrganizationResponseDTO>(entity);
        }

        public async Task<OrganizationResponseDTO> CreateAsync(CreateOrganizationDTO dto)
        {
            var entity = _mapper.Map<Organization>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Organization>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<OrganizationResponseDTO>(entity);
        }

        public async Task<OrganizationResponseDTO> UpdateAsync(Guid id, UpdateOrganizationDTO dto)
        {
            var entity = await _unitOfWork.Repository<Organization>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Organization with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Organization>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<OrganizationResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Organization>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Organization with ID {id} not found.", 404);
            _unitOfWork.Repository<Organization>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
