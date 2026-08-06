using Application.DTOs.RequestDTOs.Contract;
using Application.DTOs.ResponseDTOs.Contract;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;

using Domain.Entities;
using Domain.Enum.Audit;

namespace Application.Services
{
    public class ContractService : IContractService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLog;

        public ContractService(IUnitOfWork unitOfWork, IMapper mapper, IAuditLogService auditLog)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _auditLog = auditLog;
        }

        public async Task<ContractResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Contract>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<ContractResponseDTO>(entity);
        }

        public async Task<ContractResponseDTO> CreateAsync(CreateContractDTO dto, Guid actorId)
        {
            var entity = _mapper.Map<Contract>(dto);
            entity.Id = Guid.NewGuid();
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<Contract>().CreateAsync(entity);
            await LogAsync(AuditAction.Create, entity, actorId, $"Tạo hợp đồng '{entity.Code} - {entity.Name}'");
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ContractResponseDTO>(entity);
        }

        public async Task<ContractResponseDTO> UpdateAsync(Guid id, UpdateContractDTO dto, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<Contract>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Contract with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Contract>().Update(entity);
            await LogAsync(AuditAction.Update, entity, actorId, $"Cập nhật hợp đồng '{entity.Code} - {entity.Name}'");
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ContractResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<Contract>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Contract with ID {id} not found.", 404);
            _unitOfWork.Repository<Contract>().Delete(entity);
            await LogAsync(AuditAction.Delete, entity, actorId, $"Xoá hợp đồng '{entity.Code} - {entity.Name}'");
            await _unitOfWork.CommitAsync();
        }

        private async Task LogAsync(AuditAction action, Contract entity, Guid actorId, string detail)
        {
            var package = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(entity.ContractPackageId);
            await _auditLog.LogAsync(
                LogScope.Project, action, nameof(Contract), entity.Id.ToString(), actorId,
                detail: detail, projectId: package?.ProjectId);
        }
    }
}
