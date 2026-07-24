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
        {
            var entities = (await _unitOfWork.Repository<Organization>().GetAllAsync()).ToList();
            var jvMembers = (await _unitOfWork.Repository<JointVentureMember>().GetAllAsync()).ToList();

            var result = _mapper.Map<List<OrganizationResponseDTO>>(entities);
            foreach (var dto in result)
            {
                if (dto.IsJointVenture)
                {
                    dto.JointVentureMemberIds = jvMembers
                        .Where(j => j.JointVentureId == dto.Id)
                        .Select(j => j.MemberOrganizationId)
                        .ToList();
                }
            }
            return result;
        }

        public async Task<OrganizationResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Organization>().GetByIdAsync(id);
            if (entity == null) return null;

            var dto = _mapper.Map<OrganizationResponseDTO>(entity);
            if (dto.IsJointVenture)
            {
                var jvMembers = await _unitOfWork.Repository<JointVentureMember>().FindAsync(j => j.JointVentureId == id);
                dto.JointVentureMemberIds = jvMembers.Select(j => j.MemberOrganizationId).ToList();
            }
            return dto;
        }

        public async Task<OrganizationResponseDTO> CreateAsync(CreateOrganizationDTO dto)
        {
            if (dto == null) throw new ApiExceptionResponse("Invalid payload", 400);

            if (dto.IsJointVenture)
            {
                var existingJv = await _unitOfWork.Repository<Organization>().FindAsync(o => o.IsJointVenture && o.LegalName.ToLower() == dto.LegalName.ToLower());
                if (existingJv.Any())
                    throw new ApiExceptionResponse($"Liên danh với tên '{dto.LegalName}' đã tồn tại.", 400);
            }
            else
            {
                var existingOrg = await _unitOfWork.Repository<Organization>().FindAsync(o =>
                    !o.IsJointVenture &&
                    o.OrganizationTypeId == dto.OrganizationTypeId &&
                    (!string.IsNullOrEmpty(dto.TaxCode) ? o.TaxCode == dto.TaxCode : o.LegalName.ToLower() == dto.LegalName.ToLower())
                );
                if (existingOrg.Any())
                {
                    var msg = !string.IsNullOrEmpty(dto.TaxCode) ? $"Tổ chức với mã số thuế '{dto.TaxCode}' và vai trò này đã tồn tại." : $"Tổ chức với tên '{dto.LegalName}' và vai trò này đã tồn tại.";
                    throw new ApiExceptionResponse(msg, 400);
                }
            }

            var entity = _mapper.Map<Organization>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Organization>().CreateAsync(entity);

            if (dto.IsJointVenture && dto.JointVentureMemberIds != null)
            {
                foreach (var memberId in dto.JointVentureMemberIds.Distinct())
                {
                    await _unitOfWork.Repository<JointVentureMember>().CreateAsync(new JointVentureMember
                    {
                        Id = Guid.NewGuid(),
                        JointVentureId = entity.Id,
                        MemberOrganizationId = memberId
                    });
                }
            }

            await _unitOfWork.CommitAsync();
            return await GetByIdAsync(entity.Id) ?? _mapper.Map<OrganizationResponseDTO>(entity);
        }

        public async Task<OrganizationResponseDTO> UpdateAsync(Guid id, UpdateOrganizationDTO dto)
        {
            if (dto == null) throw new ApiExceptionResponse("Invalid payload", 400);

            var entity = await _unitOfWork.Repository<Organization>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Organization with ID {id} not found.", 404);

            var isJv = dto.IsJointVenture ?? entity.IsJointVenture;
            var legalName = dto.LegalName ?? entity.LegalName;
            var taxCode = dto.TaxCode ?? entity.TaxCode;
            var orgTypeId = dto.OrganizationTypeId ?? entity.OrganizationTypeId;

            if (isJv)
            {
                var existingJv = await _unitOfWork.Repository<Organization>().FindAsync(o => o.Id != id && o.IsJointVenture && o.LegalName.ToLower() == legalName.ToLower());
                if (existingJv.Any())
                    throw new ApiExceptionResponse($"Liên danh với tên '{legalName}' đã tồn tại.", 400);
            }
            else
            {
                var existingOrg = await _unitOfWork.Repository<Organization>().FindAsync(o =>
                    o.Id != id &&
                    !o.IsJointVenture &&
                    o.OrganizationTypeId == orgTypeId &&
                    (!string.IsNullOrEmpty(taxCode) ? o.TaxCode == taxCode : o.LegalName.ToLower() == legalName.ToLower())
                );
                if (existingOrg.Any())
                {
                    var msg = !string.IsNullOrEmpty(taxCode) ? $"Tổ chức với mã số thuế '{taxCode}' và vai trò này đã tồn tại." : $"Tổ chức với tên '{legalName}' và vai trò này đã tồn tại.";
                    throw new ApiExceptionResponse(msg, 400);
                }
            }

            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Organization>().Update(entity);

            if (dto.IsJointVenture.HasValue && dto.JointVentureMemberIds != null)
            {
                var repo = _unitOfWork.Repository<JointVentureMember>();
                var existingMembers = (await repo.FindAsync(j => j.JointVentureId == id)).ToList();

                var toRemove = existingMembers.Where(j => !dto.JointVentureMemberIds.Contains(j.MemberOrganizationId)).ToList();
                var toAddIds = dto.JointVentureMemberIds.Except(existingMembers.Select(j => j.MemberOrganizationId)).Distinct();

                foreach (var rm in toRemove) repo.Delete(rm);

                foreach (var addId in toAddIds)
                {
                    await repo.CreateAsync(new JointVentureMember
                    {
                        Id = Guid.NewGuid(),
                        JointVentureId = id,
                        MemberOrganizationId = addId
                    });
                }
            }

            await _unitOfWork.CommitAsync();
            return await GetByIdAsync(id) ?? _mapper.Map<OrganizationResponseDTO>(entity);
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
