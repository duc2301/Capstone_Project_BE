using Application.DTOs.RequestDTOs.Contract;
using Application.DTOs.ResponseDTOs.Contract;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class ContractService : IContractService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ContractService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ContractResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ContractResponseDTO>>(
                await _unitOfWork.Repository<Contract>().GetAllAsync());

        public async Task<ContractResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Contract>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<ContractResponseDTO>(entity);
        }

        public async Task<ContractResponseDTO> CreateAsync(CreateContractDTO dto)
        {
            var entity = _mapper.Map<Contract>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Contract>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ContractResponseDTO>(entity);
        }

        public async Task<ContractResponseDTO> UpdateAsync(Guid id, UpdateContractDTO dto)
        {
            var entity = await _unitOfWork.Repository<Contract>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Contract with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Contract>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ContractResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Contract>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Contract with ID {id} not found.", 404);
            _unitOfWork.Repository<Contract>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
