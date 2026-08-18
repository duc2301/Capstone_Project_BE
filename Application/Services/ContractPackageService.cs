using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.ContractPackage;
using Application.DTOs.ResponseDTOs.ContractPackage;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;

using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.ContractPackage;
using Domain.Enum.Group;
using Domain.Enum.Project;
using Application.DTOs.ResponseDTOs.Folder;

namespace Application.Services
{
    public class ContractPackageService : IContractPackageService
    {
        private const string ProjectInclude = "Project";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLog;

        public ContractPackageService(IUnitOfWork unitOfWork, IMapper mapper, IAuditLogService auditLog)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _auditLog = auditLog;
        }

        private const int DefaultPackagePageSize = 20;
        private const int MaxPackagePageSize = 500;

        public async Task<PagedResult<ContractPackageResponseDTO>> GetAllAsync(int page, int pageSize)
        {
            var packages = (await _unitOfWork.Repository<ContractPackage>().GetAllAsync(ProjectInclude))
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            var safePage = page < 1 ? 1 : page;
            var safeSize = pageSize < 1 || pageSize > MaxPackagePageSize ? DefaultPackagePageSize : pageSize;
            var pagePackages = packages.Skip((safePage - 1) * safeSize).Take(safeSize).ToList();

            var result = _mapper.Map<List<ContractPackageResponseDTO>>(pagePackages);
            await AttachAssignmentsAsync(result);

            return new PagedResult<ContractPackageResponseDTO>(result, packages.Count, safePage, safeSize);
        }

        public async Task<IEnumerable<ContractPackageResponseDTO>> GetMineAsync(Guid accountId)
        {
            var myGroupIds = (await _unitOfWork.Repository<GroupMember>()
                    .FindAsync(gm => gm.AccountId == accountId && gm.Status == GroupMemberStatus.Active))
                .Select(gm => gm.GroupId)
                .ToHashSet();

            var participantProjectIds = (await _unitOfWork.Repository<ProjectParticipant>()
                    .FindAsync(pp => myGroupIds.Contains(pp.GroupId) && pp.Status == ProjectParticipantStatus.Active))
                .Select(pp => pp.ProjectId)
                .ToHashSet();

            var projectIds = (await _unitOfWork.Repository<Project>()
                    .FindAsync(p => participantProjectIds.Contains(p.Id) || p.ManagerAccountId == accountId))
                .Select(p => p.Id)
                .ToHashSet();

            if (projectIds.Count == 0) return new List<ContractPackageResponseDTO>();

            var packages = await _unitOfWork.Repository<ContractPackage>()
                .FindAsync(p => projectIds.Contains(p.ProjectId), ProjectInclude);
            var result = _mapper.Map<List<ContractPackageResponseDTO>>(packages);
            await AttachAssignmentsAsync(result);
            return result;
        }

        public async Task<IEnumerable<ContractPackageResponseDTO>> GetByProjectIdAsync(Guid projectId)
        {
            var packages = await _unitOfWork.Repository<ContractPackage>().FindAsync(p => p.ProjectId == projectId, ProjectInclude);
            var result = _mapper.Map<List<ContractPackageResponseDTO>>(packages);
            await AttachAssignmentsAsync(result);
            return result;
        }

        public async Task<ContractPackageResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = (await _unitOfWork.Repository<ContractPackage>().FindAsync(cp => cp.Id == id, ProjectInclude)).FirstOrDefault();
            if (entity == null) return null;

            var result = _mapper.Map<ContractPackageResponseDTO>(entity);
            await AttachAssignmentsAsync(new List<ContractPackageResponseDTO> { result });
            return result;
        }

        private async Task AttachAssignmentsAsync(List<ContractPackageResponseDTO> packages)
        {
            if (packages.Count == 0) return;

            var packageIds = packages.Select(p => p.Id).ToList();
            var assignments = (await _unitOfWork.Repository<PackageAssignment>()
                .FindAsync(a => packageIds.Contains(a.ContractPackageId))).ToList();
            if (assignments.Count == 0) return;

            var orgIds = assignments.Select(a => a.OrganizationId).Distinct().ToList();
            var orgs = await _unitOfWork.Repository<Organization>().FindAsync(o => orgIds.Contains(o.Id));

            var accountIds = assignments
                .Where(a => a.RepresentativeAccountId.HasValue)
                .Select(a => a.RepresentativeAccountId.Value)
                .Distinct()
                .ToList();
            var accounts = await _unitOfWork.Repository<Account>().FindAsync(a => accountIds.Contains(a.Id));

            var assignmentsByPackage = assignments.ToLookup(a => a.ContractPackageId);

            foreach (var package in packages)
            {
                foreach (var assignment in assignmentsByPackage[package.Id])
                {
                    var org = orgs.FirstOrDefault(o => o.Id == assignment.OrganizationId);
                    var account = assignment.RepresentativeAccountId.HasValue
                        ? accounts.FirstOrDefault(x => x.Id == assignment.RepresentativeAccountId.Value)
                        : null;

                    package.Assignments.Add(new PackageAssignmentResponseDTO
                    {
                        Id = assignment.Id,
                        ContractPackageId = assignment.ContractPackageId,
                        OrganizationId = assignment.OrganizationId,
                        OrganizationName = org?.DisplayName ?? org?.LegalName,
                        OrganizationCode = org?.TaxCode,
                        Role = assignment.Role,
                        ContractNumber = assignment.ContractNumber,
                        RepresentativeAccountId = assignment.RepresentativeAccountId,
                        RepresentativeName = account?.UserName,
                        RepresentativeEmail = account?.Email,
                        RepresentativePhone = null,
                        Position = assignment.Position,
                        VatCode = assignment.VatCode,
                        ContractSignDate = assignment.ContractSignDate,
                        CreatedAt = assignment.CreatedAt
                    });
                }
            }
        }

        public async Task<ContractPackageResponseDTO> CreateAsync(CreateContractPackageDTO dto, Guid actorId)
        {
            var entity = _mapper.Map<ContractPackage>(dto);
            entity.Id = Guid.NewGuid();
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;

            // Auto-generate code if empty
            if (string.IsNullOrWhiteSpace(entity.Code))
            {
                // WorkTypes lưu MÃ loại công việc ("STR,MEP"); mã đầu tiên là phần viết tắt của mã gói thầu.
                var workTypeAbbr = WorkTypeCode.General;
                if (!string.IsNullOrWhiteSpace(dto.WorkTypes))
                {
                    var firstCode = dto.WorkTypes.Split(',')[0].Trim().ToUpperInvariant();
                    if (WorkTypeCode.IsValid(firstCode)) workTypeAbbr = firstCode;
                }

                var projectPackagesCount = (await _unitOfWork.Repository<ContractPackage>()
                    .FindAsync(p => p.ProjectId == dto.ProjectId)).Count();
                var year = DateTime.UtcNow.Year;

                var project = await _unitOfWork.Repository<Project>().GetByIdAsync(dto.ProjectId);
                var projectAbbr = "PKG";
                if (project != null && !string.IsNullOrWhiteSpace(project.ProjectName))
                {
                    var words = project.ProjectName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    projectAbbr = string.Join("", words.Select(w => char.ToUpper(w[0])));
                }

                entity.Code = $"{projectAbbr}-{year}-{workTypeAbbr}-{(projectPackagesCount + 1):D3}";
            }

            if (entity.StartDate.HasValue && entity.StartDate.Value.Kind == DateTimeKind.Unspecified)
                entity.StartDate = DateTime.SpecifyKind(entity.StartDate.Value, DateTimeKind.Utc);
            if (entity.EndDate.HasValue && entity.EndDate.Value.Kind == DateTimeKind.Unspecified)
                entity.EndDate = DateTime.SpecifyKind(entity.EndDate.Value, DateTimeKind.Utc);

            entity.Status = ContractPackage.DeriveStatus(entity.StartDate, entity.EndDate);

            // Assign contractor if provided
            if (dto.ContractorOrganizationId.HasValue)
            {
                var signDate = dto.ContractSignDate;
                if (signDate.HasValue && signDate.Value.Kind == DateTimeKind.Unspecified)
                    signDate = DateTime.SpecifyKind(signDate.Value, DateTimeKind.Utc);

                var assignment = new PackageAssignment
                {
                    Id = Guid.NewGuid(),
                    ContractPackageId = entity.Id,
                    OrganizationId = dto.ContractorOrganizationId.Value,
                    Role = Domain.Enum.ContractPackage.PackageRole.MainContractor,
                    RepresentativeAccountId = dto.RepresentativeAccountId,
                    ContractNumber = dto.ContractNumber,
                    ContractSignDate = signDate,
                    Position = dto.ContractJobTitle,
                    CreatedAt = DateTime.UtcNow
                };
                entity.Assignments.Add(assignment);
            }

            var rootPublished = (await _unitOfWork.Repository<Folder>()
                .FindAsync(f => f.ProjectId == dto.ProjectId && f.Area == Domain.Enum.Cde.CdeArea.Published && f.ParentFolderId == null))
                .FirstOrDefault();

            if (rootPublished != null)
            {
                var packageMasterFolder = (await _unitOfWork.Repository<Folder>()
                    .FindAsync(f => f.ParentFolderId == rootPublished.Id && f.Name == FolderBootstrapService.ContractPackagesFolderName))
                    .FirstOrDefault();

                if (packageMasterFolder != null)
                {
                    var folderName = entity.Name.Trim();
                    var existingFolder = (await _unitOfWork.Repository<Folder>()
                        .FindAsync(f => f.ParentFolderId == packageMasterFolder.Id && f.Name.ToLower() == folderName.ToLower())).FirstOrDefault();
                    
                    if (existingFolder == null)
                    {
                        var newFolder = new Folder
                        {
                            Id = Guid.NewGuid(),
                            ProjectId = dto.ProjectId,
                            ParentFolderId = packageMasterFolder.Id,
                            Name = folderName,
                            Area = Domain.Enum.Cde.CdeArea.Published,
                            IsTemplate = false,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.Repository<Folder>().CreateAsync(newFolder);
                        
                        // Assign the folder to the contract package so we know where its documents go
                        entity.DocumentFolderId = newFolder.Id;
                    }
                }
            }

            try
            {
                await _unitOfWork.Repository<ContractPackage>().CreateAsync(entity);
                await _auditLog.LogAsync(
                    LogScope.Project, AuditAction.Create, nameof(ContractPackage), entity.Id.ToString(), actorId,
                    detail: $"Tạo gói thầu '{entity.Code} - {entity.Name}'", projectId: entity.ProjectId);
                await _unitOfWork.CommitAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new ApiExceptionResponse($"DbUpdateException: {innerMsg}", 400);
            }
            
            return _mapper.Map<ContractPackageResponseDTO>(entity);
        }

        public async Task<ContractPackageResponseDTO> UpdateAsync(Guid id, UpdateContractPackageDTO dto, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ContractPackage with ID {id} not found.", 404);

            _mapper.Map(dto, entity);
            entity.UpdatedAt = DateTime.UtcNow;

            if (entity.StartDate.HasValue && entity.StartDate.Value.Kind == DateTimeKind.Unspecified)
                entity.StartDate = DateTime.SpecifyKind(entity.StartDate.Value, DateTimeKind.Utc);
            if (entity.EndDate.HasValue && entity.EndDate.Value.Kind == DateTimeKind.Unspecified)
                entity.EndDate = DateTime.SpecifyKind(entity.EndDate.Value, DateTimeKind.Utc);
            if (entity.CreatedAt.HasValue && entity.CreatedAt.Value.Kind == DateTimeKind.Unspecified)
                entity.CreatedAt = DateTime.SpecifyKind(entity.CreatedAt.Value, DateTimeKind.Utc);

            entity.Status = ContractPackage.DeriveStatus(entity.StartDate, entity.EndDate);
            _unitOfWork.Repository<ContractPackage>().Update(entity);

            // Update assignment if ContractorOrganizationId is provided
            if (dto.ContractorOrganizationId.HasValue || dto.RepresentativeAccountId.HasValue || dto.ContractNumber != null || dto.ContractSignDate.HasValue || dto.ContractJobTitle != null)
            {
                var existingAssignment = await _unitOfWork.Repository<PackageAssignment>()
                    .FindAsync(a => a.ContractPackageId == id && a.Role == Domain.Enum.ContractPackage.PackageRole.MainContractor);
                
                var assignment = existingAssignment.FirstOrDefault();

                var isNewAssignment = false;
                if (assignment == null && dto.ContractorOrganizationId.HasValue)
                {
                    assignment = new PackageAssignment
                    {
                        Id = Guid.NewGuid(),
                        ContractPackageId = id,
                        OrganizationId = dto.ContractorOrganizationId.Value,
                        Role = Domain.Enum.ContractPackage.PackageRole.MainContractor,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<PackageAssignment>().CreateAsync(assignment);
                    isNewAssignment = true;
                }

                if (assignment != null)
                {
                    if (dto.ContractorOrganizationId.HasValue) assignment.OrganizationId = dto.ContractorOrganizationId.Value;
                    if (dto.RepresentativeAccountId.HasValue) assignment.RepresentativeAccountId = dto.RepresentativeAccountId;
                    if (dto.ContractNumber != null) assignment.ContractNumber = dto.ContractNumber;
                    if (dto.ContractSignDate.HasValue)
                    {
                        var signDate = dto.ContractSignDate.Value;
                        if (signDate.Kind == DateTimeKind.Unspecified)
                            signDate = DateTime.SpecifyKind(signDate, DateTimeKind.Utc);
                        assignment.ContractSignDate = signDate;
                    }
                    if (dto.ContractJobTitle != null) assignment.Position = dto.ContractJobTitle;

                    if (!isNewAssignment)
                        _unitOfWork.Repository<PackageAssignment>().Update(assignment);
                }
            }

            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Update, nameof(ContractPackage), entity.Id.ToString(), actorId,
                detail: $"Cập nhật gói thầu '{entity.Code} - {entity.Name}'", projectId: entity.ProjectId);

            try
            {
                await _unitOfWork.CommitAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new ApiExceptionResponse($"DbUpdateException: {innerMsg}", 400);
            }

            return await GetByIdAsync(id) ?? _mapper.Map<ContractPackageResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ContractPackage with ID {id} not found.", 404);
            _unitOfWork.Repository<ContractPackage>().Delete(entity);
            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Delete, nameof(ContractPackage), entity.Id.ToString(), actorId,
                detail: $"Xoá gói thầu '{entity.Code} - {entity.Name}'", projectId: entity.ProjectId);
            await _unitOfWork.CommitAsync();
        }

        public async Task<FolderResponseDTO> CreateContractorWipFolderAsync(Guid projectId, string contractorName, Guid actorId)
        {
            if (string.IsNullOrWhiteSpace(contractorName))
                throw new ApiExceptionResponse("Contractor name is required.", 400);

            // 1. Find the root WIP folder for the project
            var rootWip = (await _unitOfWork.Repository<Folder>()
                .FindAsync(f => f.ProjectId == projectId && f.Area == Domain.Enum.Cde.CdeArea.Wip && f.ParentFolderId == null))
                .FirstOrDefault() ?? throw new ApiExceptionResponse("WIP root folder not initialized for this project.", 404);

            // 2. Check if a folder for this contractor already exists
            var existingFolder = (await _unitOfWork.Repository<Folder>()
                .FindAsync(f => f.ParentFolderId == rootWip.Id && f.Name.ToLower() == contractorName.ToLower()))
                .FirstOrDefault();

            if (existingFolder != null)
                return _mapper.Map<Application.DTOs.ResponseDTOs.Folder.FolderResponseDTO>(existingFolder);

            // 3. Create the new folder
            var now = DateTime.UtcNow;
            var child = new Folder
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentFolderId = rootWip.Id,
                Name = contractorName.Trim(),
                Area = Domain.Enum.Cde.CdeArea.Wip,
                IsTemplate = false,
                CreatedByAccountId = actorId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _unitOfWork.Repository<Folder>().CreateAsync(child);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<Application.DTOs.ResponseDTOs.Folder.FolderResponseDTO>(child);
        }
    }
}
