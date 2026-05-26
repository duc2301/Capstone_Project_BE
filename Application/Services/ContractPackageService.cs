using Application.DTOs.RequestDTOs.ContractPackage;
using Application.DTOs.ResponseDTOs.ContractPackage;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class ContractPackageService : IContractPackageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ContractPackageService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ContractPackageResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ContractPackageResponseDTO>>(
                await _unitOfWork.Repository<ContractPackage>().GetAllAsync());

        public async Task<ContractPackageResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<ContractPackageResponseDTO>(entity);
        }

        public async Task<ContractPackageResponseDTO> CreateAsync(CreateContractPackageDTO dto)
        {
            var entity = _mapper.Map<ContractPackage>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<ContractPackage>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ContractPackageResponseDTO>(entity);
        }

        public async Task<ContractPackageResponseDTO> UpdateAsync(Guid id, UpdateContractPackageDTO dto)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ContractPackage with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<ContractPackage>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ContractPackageResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ContractPackage with ID {id} not found.", 404);
            _unitOfWork.Repository<ContractPackage>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
