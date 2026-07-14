using Application.DTOs.RequestDTOs.ContractPackage;
using Application.DTOs.ResponseDTOs.ContractPackage;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;
using Application.DTOs.ResponseDTOs.Folder;

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

        public async Task<IEnumerable<ContractPackageResponseDTO>> GetByProjectIdAsync(Guid projectId)
            => _mapper.Map<IEnumerable<ContractPackageResponseDTO>>(
                await _unitOfWork.Repository<ContractPackage>().FindAsync(p => p.ProjectId == projectId));

        public async Task<ContractPackageResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id);
            if (entity == null) return null;

            var result = _mapper.Map<ContractPackageResponseDTO>(entity);

            var assignments = await _unitOfWork.Repository<PackageAssignment>().FindAsync(x => x.ContractPackageId == id);
            if (assignments.Any())
            {
                var orgIds = assignments.Select(a => a.OrganizationId).Distinct().ToList();
                var orgs = await _unitOfWork.Repository<Organization>().FindAsync(o => orgIds.Contains(o.Id));
                
                var accIds = assignments.Where(a => a.RepresentativeAccountId.HasValue).Select(a => a.RepresentativeAccountId.Value).Distinct().ToList();
                var accounts = await _unitOfWork.Repository<Account>().FindAsync(a => accIds.Contains(a.Id));

                foreach (var a in assignments)
                {
                    var org = orgs.FirstOrDefault(o => o.Id == a.OrganizationId);
                    var acc = a.RepresentativeAccountId.HasValue ? accounts.FirstOrDefault(x => x.Id == a.RepresentativeAccountId.Value) : null;
                    
                    result.Assignments.Add(new PackageAssignmentResponseDTO
                    {
                        Id = a.Id,
                        ContractPackageId = a.ContractPackageId,
                        OrganizationId = a.OrganizationId,
                        OrganizationName = org?.DisplayName ?? org?.LegalName,
                        OrganizationCode = org?.TaxCode,
                        Role = a.Role,
                        ContractNumber = a.ContractNumber,
                        RepresentativeAccountId = a.RepresentativeAccountId,
                        RepresentativeName = acc != null ? acc.UserName : null,
                        RepresentativeEmail = acc?.Email,
                        RepresentativePhone = null,
                        Position = a.Position,
                        VatCode = a.VatCode,
                        ContractSignDate = a.ContractSignDate,
                        CreatedAt = a.CreatedAt
                    });
                }
            }

            return result;
        }

        public async Task<ContractPackageResponseDTO> CreateAsync(CreateContractPackageDTO dto)
        {
            var entity = _mapper.Map<ContractPackage>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }

            // Auto-generate code if empty
            if (string.IsNullOrWhiteSpace(entity.Code))
            {
                // Derive work-type abbreviation from WorkTypes field
                var workTypeAbbr = "GEN"; // default = General
                if (!string.IsNullOrWhiteSpace(dto.WorkTypes))
                {
                    var firstType = dto.WorkTypes.Split(',')[0].Trim().ToLower();
                    workTypeAbbr = firstType switch
                    {
                        "xây dựng thô" or "xây dựng" => "XDT",
                        "kết cấu" or "structure" => "STR",
                        "kiến trúc" or "architecture" => "ARC",
                        "cơ điện" or "m&e" or "mep" => "MEP",
                        "hoàn thiện" or "finishing" => "FIN",
                        "bê tông cốt thép" => "STR",
                        _ => firstType.Length >= 3
                            ? firstType[..3].ToUpper()
                            : firstType.ToUpper()
                    };
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

                // Auto-create WIP folder for this contractor
                var org = await _unitOfWork.Repository<Organization>().GetByIdAsync(dto.ContractorOrganizationId.Value);
                if (org != null)
                {
                    var rootWip = (await _unitOfWork.Repository<Folder>()
                        .FindAsync(f => f.ProjectId == dto.ProjectId && f.Area == Domain.Enum.Cde.CdeArea.Wip && f.ParentFolderId == null))
                        .FirstOrDefault();
                    if (rootWip != null)
                    {
                        var folderName = (org.DisplayName ?? org.LegalName).Trim();
                        var existingFolder = (await _unitOfWork.Repository<Folder>()
                            .FindAsync(f => f.ParentFolderId == rootWip.Id && f.Name.ToLower() == folderName.ToLower())).FirstOrDefault();
                        if (existingFolder == null)
                        {
                            var newFolder = new Folder
                            {
                                Id = Guid.NewGuid(),
                                ProjectId = dto.ProjectId,
                                ParentFolderId = rootWip.Id,
                                Name = folderName,
                                Area = Domain.Enum.Cde.CdeArea.Wip,
                                IsTemplate = false,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            await _unitOfWork.Repository<Folder>().CreateAsync(newFolder);
                        }
                    }
                }
            }

            try
            {
                await _unitOfWork.Repository<ContractPackage>().CreateAsync(entity);
                await _unitOfWork.CommitAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new ApiExceptionResponse($"DbUpdateException: {innerMsg}", 400);
            }
            
            return _mapper.Map<ContractPackageResponseDTO>(entity);
        }

        public async Task<ContractPackageResponseDTO> UpdateAsync(Guid id, UpdateContractPackageDTO dto)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ContractPackage with ID {id} not found.", 404);

            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;

            if (entity.StartDate.HasValue && entity.StartDate.Value.Kind == DateTimeKind.Unspecified)
                entity.StartDate = DateTime.SpecifyKind(entity.StartDate.Value, DateTimeKind.Utc);
            if (entity.EndDate.HasValue && entity.EndDate.Value.Kind == DateTimeKind.Unspecified)
                entity.EndDate = DateTime.SpecifyKind(entity.EndDate.Value, DateTimeKind.Utc);
            if (entity.CreatedAt.HasValue && entity.CreatedAt.Value.Kind == DateTimeKind.Unspecified)
                entity.CreatedAt = DateTime.SpecifyKind(entity.CreatedAt.Value, DateTimeKind.Utc);
            _unitOfWork.Repository<ContractPackage>().Update(entity);

            // Update assignment if ContractorOrganizationId is provided
            if (dto.ContractorOrganizationId.HasValue)
            {
                var assignments = await _unitOfWork.Repository<PackageAssignment>()
                    .FindAsync(a => a.ContractPackageId == id && a.Role == Domain.Enum.ContractPackage.PackageRole.MainContractor);
                var mainAssignment = assignments.FirstOrDefault();

                var signDate = dto.ContractSignDate;
                if (signDate.HasValue && signDate.Value.Kind == DateTimeKind.Unspecified)
                    signDate = DateTime.SpecifyKind(signDate.Value, DateTimeKind.Utc);

                if (mainAssignment != null)
                {
                    mainAssignment.OrganizationId = dto.ContractorOrganizationId.Value;
                    mainAssignment.RepresentativeAccountId = dto.RepresentativeAccountId;
                    mainAssignment.ContractNumber = dto.ContractNumber;
                    mainAssignment.ContractSignDate = signDate;
                    mainAssignment.Position = dto.ContractJobTitle;
                    _unitOfWork.Repository<PackageAssignment>().Update(mainAssignment);
                }
                else
                {
                    mainAssignment = new PackageAssignment
                    {
                        Id = Guid.NewGuid(),
                        ContractPackageId = id,
                        OrganizationId = dto.ContractorOrganizationId.Value,
                        Role = Domain.Enum.ContractPackage.PackageRole.MainContractor,
                        RepresentativeAccountId = dto.RepresentativeAccountId,
                        ContractNumber = dto.ContractNumber,
                        ContractSignDate = signDate,
                        Position = dto.ContractJobTitle,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<PackageAssignment>().CreateAsync(mainAssignment);
                }
            }

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

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ContractPackage>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ContractPackage with ID {id} not found.", 404);
            _unitOfWork.Repository<ContractPackage>().Delete(entity);
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
