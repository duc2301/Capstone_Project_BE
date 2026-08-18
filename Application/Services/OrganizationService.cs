using Application.DTOs.RequestDTOs.Organization;
using Application.DTOs.ResponseDTOs.Organization;
using Application.DTOs.ResponseDTOs.Project;
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
        private readonly IProjectService _projectService;

        public OrganizationService(IUnitOfWork unitOfWork, IMapper mapper, IProjectService projectService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _projectService = projectService;
        }

        private const int DefaultOrganizationPageSize = 20;
        private const int MaxOrganizationPageSize = 500;

        public async Task<OrganizationPageDTO> GetAllAsync(int page, int pageSize)
        {
            var entities = (await _unitOfWork.Repository<Organization>().GetAllAsync())
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var safePage = page < 1 ? 1 : page;
            var safeSize = pageSize < 1 || pageSize > MaxOrganizationPageSize ? DefaultOrganizationPageSize : pageSize;
            var pageEntities = entities.Skip((safePage - 1) * safeSize).Take(safeSize).ToList();

            var result = _mapper.Map<List<OrganizationResponseDTO>>(pageEntities);
            var orgIds = result.Select(dto => dto.Id).ToList();

            var jvMembersById = (await _unitOfWork.Repository<JointVentureMember>()
                    .FindAsync(j => orgIds.Contains(j.JointVentureId)))
                .GroupBy(j => j.JointVentureId)
                .ToDictionary(g => g.Key, g => g.Select(j => j.MemberOrganizationId).ToList());

            var counts = await ComputeParticipatingProjectsCountsAsync(orgIds);

            foreach (var dto in result)
            {
                if (dto.IsJointVenture && jvMembersById.TryGetValue(dto.Id, out var memberIds))
                    dto.JointVentureMemberIds = memberIds;

                dto.ParticipatingProjectsCount = counts.GetValueOrDefault(dto.Id);
            }

            return new OrganizationPageDTO
            {
                Items = result,
                Total = entities.Count,
                Page = safePage,
                PageSize = safeSize
            };
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

            var counts = await ComputeParticipatingProjectsCountsAsync(new[] { id });
            dto.ParticipatingProjectsCount = counts.GetValueOrDefault(id);

            return dto;
        }

        /// <summary>
        /// Đếm số project mà mỗi tổ chức (trong orgIds) đang tham gia/sở hữu/quản lý: project của nhóm
        /// thuộc tổ chức hoặc nhóm có thành viên thuộc tổ chức (participant), project tổ chức sở hữu
        /// (OwnerOrganizationId), hoặc project do thành viên tổ chức làm PM (ManagerAccountId). Chỉ query
        /// đúng phạm vi orgIds thay vì kéo nguyên bảng Group/Account/GroupMember/ProjectParticipant/Project.
        /// </summary>
        private async Task<Dictionary<Guid, int>> ComputeParticipatingProjectsCountsAsync(IReadOnlyCollection<Guid> orgIds)
        {
            if (orgIds.Count == 0) return new Dictionary<Guid, int>();

            var orgGroups = (await _unitOfWork.Repository<Group>()
                    .FindAsync(g => g.OrganizationId.HasValue && orgIds.Contains(g.OrganizationId.Value)))
                .ToList();
            var orgAccounts = (await _unitOfWork.Repository<Account>()
                    .FindAsync(a => a.OrganizationId.HasValue && orgIds.Contains(a.OrganizationId.Value)))
                .ToList();
            var accountIds = orgAccounts.Select(a => a.Id).ToHashSet();

            var groupIdsByOrg = orgGroups
                .GroupBy(g => g.OrganizationId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

            if (accountIds.Count > 0)
            {
                var groupMembers = await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => accountIds.Contains(gm.AccountId));
                var orgIdByAccount = orgAccounts.ToDictionary(a => a.Id, a => a.OrganizationId!.Value);

                foreach (var member in groupMembers)
                {
                    if (!orgIdByAccount.TryGetValue(member.AccountId, out var orgId)) continue;
                    if (!groupIdsByOrg.TryGetValue(orgId, out var set))
                        groupIdsByOrg[orgId] = set = new HashSet<Guid>();
                    set.Add(member.GroupId);
                }
            }

            var allGroupIds = groupIdsByOrg.Values.SelectMany(set => set).ToHashSet();

            var participants = allGroupIds.Count == 0
                ? new List<ProjectParticipant>()
                : (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(p => allGroupIds.Contains(p.GroupId))).ToList();

            var ownedOrManagedProjects = (await _unitOfWork.Repository<Project>()
                    .FindAsync(p => (p.OwnerOrganizationId.HasValue && orgIds.Contains(p.OwnerOrganizationId.Value))
                                    || (p.ManagerAccountId.HasValue && accountIds.Contains(p.ManagerAccountId.Value))))
                .ToList();

            var result = new Dictionary<Guid, int>();
            foreach (var orgId in orgIds)
            {
                var groupIds = groupIdsByOrg.TryGetValue(orgId, out var gset) ? gset : new HashSet<Guid>();
                var participatingProjectIds = participants.Where(p => groupIds.Contains(p.GroupId)).Select(p => p.ProjectId);

                var orgAccountIdSet = orgAccounts.Where(a => a.OrganizationId == orgId).Select(a => a.Id).ToHashSet();
                var ownedProjectIds = ownedOrManagedProjects.Where(p => p.OwnerOrganizationId == orgId).Select(p => p.Id);
                var managedProjectIds = ownedOrManagedProjects
                    .Where(p => p.ManagerAccountId.HasValue && orgAccountIdSet.Contains(p.ManagerAccountId.Value))
                    .Select(p => p.Id);

                result[orgId] = participatingProjectIds.Union(ownedProjectIds).Union(managedProjectIds).Distinct().Count();
            }

            return result;
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

            var groupMembers = accountIds.Count == 0
                ? new List<GroupMember>()
                : (await _unitOfWork.Repository<GroupMember>().FindAsync(gm => accountIds.Contains(gm.AccountId))).ToList();
            var groupIdsWithOrgMembers = groupMembers.Select(gm => gm.GroupId);

            var allGroupIds = orgGroupIds.Union(groupIdsWithOrgMembers).Distinct().ToList();

            var allParticipants = allGroupIds.Count == 0
                ? new List<ProjectParticipant>()
                : (await _unitOfWork.Repository<ProjectParticipant>().FindAsync(p => allGroupIds.Contains(p.GroupId))).ToList();
            var participatingProjectIds = allParticipants.Select(p => p.ProjectId).ToList();

            var allProjectsManagedByMembers = accountIds.Count == 0
                ? new List<Project>()
                : (await _unitOfWork.Repository<Project>().FindAsync(p => p.ManagerAccountId != null && accountIds.Contains(p.ManagerAccountId.Value))).ToList();
            var managedProjectIds = allProjectsManagedByMembers.Select(p => p.Id).ToList();

            var allProjectIds = participatingProjectIds.Union(ownedProjects.Select(p => p.Id)).Union(managedProjectIds).Distinct().ToList();

            return await _projectService.GetByIdsAsync(allProjectIds);
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
