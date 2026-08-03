using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.ResponseDTOs.Organization;
using Application.DTOs.ResponseDTOs.Project;
using Application.DTOs.ResponseDTOs.Organization;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;

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
            var allProjects = (await _unitOfWork.Repository<Project>().GetAllAsync()).ToList();
            var allParticipants = (await _unitOfWork.Repository<ProjectParticipant>().GetAllAsync()).ToList();
            var allGroups = (await _unitOfWork.Repository<Group>().GetAllAsync()).ToList();
            var allAccounts = (await _unitOfWork.Repository<Account>().GetAllAsync()).ToList();
            var allGroupMembers = (await _unitOfWork.Repository<GroupMember>().GetAllAsync()).ToList();

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

                var orgGroups = allGroups.Where(g => g.OrganizationId == dto.Id).Select(g => g.Id).ToList();
                
                var orgAccountIds = allAccounts.Where(a => a.OrganizationId == dto.Id).Select(a => a.Id).ToList();
                var groupIdsWithOrgMembers = allGroupMembers.Where(gm => orgAccountIds.Contains(gm.AccountId)).Select(gm => gm.GroupId).ToList();
                
                var allGroupIds = orgGroups.Union(groupIdsWithOrgMembers).Distinct().ToList();

                var participatingProjectIds = allParticipants.Where(p => allGroupIds.Contains(p.GroupId)).Select(p => p.ProjectId);
                var ownedProjectIds = allProjects.Where(p => p.OwnerOrganizationId == dto.Id).Select(p => p.Id);
                var managedProjectIds = allProjects.Where(p => p.ManagerAccountId != null && orgAccountIds.Contains(p.ManagerAccountId.Value)).Select(p => p.Id);
                
                dto.ParticipatingProjectsCount = participatingProjectIds.Union(ownedProjectIds).Union(managedProjectIds).Distinct().Count();
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

            var allProjects = await _unitOfWork.Repository<Project>().FindAsync(p => p.OwnerOrganizationId == id);
            var ownedProjectIds = allProjects.Select(p => p.Id);

            var orgGroups = await _unitOfWork.Repository<Group>().FindAsync(g => g.OrganizationId == id);
            var orgGroupIds = orgGroups.Select(g => g.Id).ToList();

            var allParticipants = await _unitOfWork.Repository<ProjectParticipant>().GetAllAsync(); 
            
            var accountsInOrg = await _unitOfWork.Repository<Account>().FindAsync(a => a.OrganizationId == id);
            var accountIds = accountsInOrg.Select(a => a.Id).ToList();
            
            var groupMembers = await _unitOfWork.Repository<GroupMember>().GetAllAsync();
            var groupIdsWithOrgMembers = groupMembers.Where(gm => accountIds.Contains(gm.AccountId)).Select(gm => gm.GroupId).ToList();
            
            var allGroupIds = orgGroupIds.Union(groupIdsWithOrgMembers).Distinct().ToList();

            var participatingProjectIds = allParticipants.Where(p => allGroupIds.Contains(p.GroupId)).Select(p => p.ProjectId);
            
            var allProjectsManagedByMembers = await _unitOfWork.Repository<Project>().FindAsync(p => p.ManagerAccountId != null && accountIds.Contains(p.ManagerAccountId.Value));
            var managedProjectIds = allProjectsManagedByMembers.Select(p => p.Id);

            dto.ParticipatingProjectsCount = participatingProjectIds.Union(ownedProjectIds).Union(managedProjectIds).Distinct().Count();

            return dto;
        }

        public async Task<IEnumerable<ProjectResponseDTO>> GetProjectsByOrganizationAsync(Guid id)
        {
            var org = await _unitOfWork.Repository<Organization>().GetByIdAsync(id);
            if (org == null) throw new ApiExceptionResponse("Không tìm thấy tổ chức", 404);

            var ownedProjects = await _unitOfWork.Repository<Project>().FindAsync(p => p.OwnerOrganizationId == id);
            
            var orgGroups = await _unitOfWork.Repository<Group>().FindAsync(g => g.OrganizationId == id);
            var orgGroupIds = orgGroups.Select(g => g.Id).ToList();
            
            var accountsInOrg = await _unitOfWork.Repository<Account>().FindAsync(a => a.OrganizationId == id);
            var accountIds = accountsInOrg.Select(a => a.Id).ToList();
            
            var groupMembers = await _unitOfWork.Repository<GroupMember>().GetAllAsync();
            var groupIdsWithOrgMembers = groupMembers.Where(gm => accountIds.Contains(gm.AccountId)).Select(gm => gm.GroupId).ToList();
            
            var allGroupIds = orgGroupIds.Union(groupIdsWithOrgMembers).Distinct().ToList();
            
            var allParticipants = await _unitOfWork.Repository<ProjectParticipant>().GetAllAsync();
            var participatingProjectIds = allParticipants.Where(p => allGroupIds.Contains(p.GroupId)).Select(p => p.ProjectId).ToList();
            
            var allProjectsManagedByMembers = await _unitOfWork.Repository<Project>().FindAsync(p => p.ManagerAccountId != null && accountIds.Contains(p.ManagerAccountId.Value));
            var managedProjectIds = allProjectsManagedByMembers.Select(p => p.Id).ToList();
            
            var allProjectIds = participatingProjectIds.Union(ownedProjects.Select(p => p.Id)).Union(managedProjectIds).Distinct().ToList();
            
            var allParticipatingProjects = new List<Project>();
            if (allProjectIds.Any())
            {
                var dict = (await _unitOfWork.Repository<Project>().GetAllAsync()).ToDictionary(p => p.Id);
                foreach (var pid in allProjectIds)
                {
                    if (dict.TryGetValue(pid, out var prj))
                        allParticipatingProjects.Add(prj);
                }
            }

            return _mapper.Map<List<ProjectResponseDTO>>(allParticipatingProjects);
        }

        public async Task<OrganizationResponseDTO> CreateAsync(CreateOrganizationDTO dto)
        {
            if (dto == null) throw new ApiExceptionResponse("Invalid payload", 400);

            if (dto.IsJointVenture)
            {
                var existingJv = await _unitOfWork.Repository<Organization>().FindAsync(o => 
                    o.IsJointVenture && 
                    o.OrganizationTypeId == dto.OrganizationTypeId &&
                    o.LegalName.ToLower() == dto.LegalName.ToLower());
                if (existingJv.Any())
                    throw new ApiExceptionResponse($"Liên danh với tên '{dto.LegalName}' và vai trò này đã tồn tại.", 400);
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
            if (entity.IsJointVenture && string.IsNullOrEmpty(entity.TaxCode))
            {
                entity.TaxCode = string.Empty;
            }
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
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
                var existingJv = await _unitOfWork.Repository<Organization>().FindAsync(o => 
                    o.Id != id && 
                    o.IsJointVenture && 
                    o.OrganizationTypeId == orgTypeId &&
                    o.LegalName.ToLower() == legalName.ToLower());
                if (existingJv.Any())
                    throw new ApiExceptionResponse($"Liên danh với tên '{legalName}' và vai trò này đã tồn tại.", 400);
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
            if (entity.IsJointVenture && string.IsNullOrEmpty(entity.TaxCode))
            {
                entity.TaxCode = string.Empty;
            }
            entity.UpdatedAt = DateTime.UtcNow;
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
