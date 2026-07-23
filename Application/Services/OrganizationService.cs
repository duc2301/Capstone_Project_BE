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
            var entity = await _unitOfWork.Repository<Organization>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Organization with ID {id} not found.", 404);
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
