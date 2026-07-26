using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;

using Domain.Entities;
using Domain.Enum.Audit;

namespace Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IFolderBootstrapService _folderBootstrap;
        private readonly IAuditLogService _auditLog;

        public ProjectService(IUnitOfWork unitOfWork, IMapper mapper, IFolderBootstrapService folderBootstrap,
            IAuditLogService auditLog)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _folderBootstrap = folderBootstrap;
            _auditLog = auditLog;
        }

        private const string OwnerInclude = "OwnerOrganization";

        public async Task<IEnumerable<ProjectResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ProjectResponseDTO>>(
                await _unitOfWork.Repository<Project>().GetAllAsync(OwnerInclude));

        public async Task<ProjectResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = (await _unitOfWork.Repository<Project>()
                    .FindAsync(p => p.Id == id, OwnerInclude))
                .FirstOrDefault();
            if (entity == null) return null;

            var dto = _mapper.Map<ProjectResponseDTO>(entity);
            dto.Location = await GetDefaultLocationAsync(id);
            return dto;
        }

        public async Task<ProjectResponseDTO> CreateAsync(CreateProjectDTO dto, Guid actorId)
        {
            var owner = await ResolveOwnerOrganizationAsync(dto.OwnerOrganizationId);

            var entity = _mapper.Map<Project>(dto);
            entity.Id = Guid.NewGuid();
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<Project>().CreateAsync(entity);

            if (!string.IsNullOrWhiteSpace(dto.Address) || dto.Latitude.HasValue || dto.Longitude.HasValue)
            {
                await _unitOfWork.Repository<ProjectLocation>().CreateAsync(new ProjectLocation
                {
                    Id = Guid.NewGuid(),
                    ProjectId = entity.Id,
                    Address = dto.Address,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Create, nameof(Project), entity.Id.ToString(),
                actorId, detail: $"Tạo dự án '{entity.ProjectName}'", projectId: entity.Id);

            await _unitOfWork.CommitAsync();

            // Dựng 4 khu vực CDE gốc (WIP/Shared/Published/Archived) ngay khi tạo dự án.
            await _folderBootstrap.InitializeRootFoldersAsync(entity.Id);

            var result = _mapper.Map<ProjectResponseDTO>(entity);
            result.OwnerOrganizationName = owner == null ? null : (owner.DisplayName ?? owner.LegalName);
            result.Location = await GetDefaultLocationAsync(entity.Id);
            return result;
        }

        private async Task<Organization?> ResolveOwnerOrganizationAsync(Guid? organizationId)
        {
            if (!organizationId.HasValue) return null;

            return await _unitOfWork.Repository<Organization>().GetByIdAsync(organizationId.Value)
                ?? throw new ApiExceptionResponse("Owner organization not found.", 404);
        }

        private async Task<ProjectLocationResponseDTO?> GetDefaultLocationAsync(Guid projectId)
        {
            var locations = (await _unitOfWork.Repository<ProjectLocation>()
                    .FindAsync(l => l.ProjectId == projectId))
                .ToList();
            var location = locations.FirstOrDefault(l => l.IsDefault) ?? locations.FirstOrDefault();
            return location == null ? null : _mapper.Map<ProjectLocationResponseDTO>(location);
        }

        private async Task UpsertDefaultLocationAsync(Guid projectId, UpdateProjectDTO dto)
        {
            var hasLocationInput = !string.IsNullOrWhiteSpace(dto.Address)
                                   || dto.Latitude.HasValue
                                   || dto.Longitude.HasValue;
            if (!hasLocationInput) return;

            var locations = (await _unitOfWork.Repository<ProjectLocation>()
                    .FindAsync(l => l.ProjectId == projectId))
                .ToList();
            var location = locations.FirstOrDefault(l => l.IsDefault) ?? locations.FirstOrDefault();

            if (location == null)
            {
                await _unitOfWork.Repository<ProjectLocation>().CreateAsync(new ProjectLocation
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Address = dto.Address,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow
                });
                return;
            }

            if (!string.IsNullOrWhiteSpace(dto.Address)) location.Address = dto.Address;
            if (dto.Latitude.HasValue) location.Latitude = dto.Latitude;
            if (dto.Longitude.HasValue) location.Longitude = dto.Longitude;
            _unitOfWork.Repository<ProjectLocation>().Update(location);
        }

        public async Task<ProjectResponseDTO> UpdateAsync(Guid id, UpdateProjectDTO dto, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<Project>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Project with ID {id} not found.", 404);

            _ = await ResolveOwnerOrganizationAsync(dto.OwnerOrganizationId);

            _mapper.Map(dto, entity);
            entity.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Project>().Update(entity);

            await UpsertDefaultLocationAsync(id, dto);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Update, nameof(Project), entity.Id.ToString(),
                actorId, detail: $"Cập nhật dự án '{entity.ProjectName}' (trạng thái: {entity.Status}, vào lúc: {entity.UpdatedAt})",
                projectId: entity.Id);

            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(id) ?? _mapper.Map<ProjectResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id, Guid actorId)
        {
            var entity = await _unitOfWork.Repository<Project>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Project with ID {id} not found.", 404);
            _unitOfWork.Repository<Project>().Delete(entity);

            await _auditLog.LogAsync(
                LogScope.System, AuditAction.Delete, nameof(Project), entity.Id.ToString(),
                actorId, detail: $"Xoá dự án '{entity.ProjectName}'", projectId: entity.Id);

            await _unitOfWork.CommitAsync();
        }
    }
}
