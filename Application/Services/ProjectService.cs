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
        private readonly IImageUploadService _imageUpload;

        public ProjectService(IUnitOfWork unitOfWork, IMapper mapper, IFolderBootstrapService folderBootstrap,
            IAuditLogService auditLog, IImageUploadService imageUpload)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _folderBootstrap = folderBootstrap;
            _auditLog = auditLog;
            _imageUpload = imageUpload;
        }

        private const string OwnerInclude = "OwnerOrganization";
        private const string ProjectImagePrefix = "project-images";

        public async Task<IEnumerable<ProjectResponseDTO>> GetAllAsync()
        {
            var entities = (await _unitOfWork.Repository<Project>().GetAllAsync(OwnerInclude)).ToList();
            return await BuildListAsync(entities);
        }

        public async Task<List<ProjectResponseDTO>> GetByIdsAsync(IReadOnlyCollection<Guid> ids)
        {
            if (ids.Count == 0) return new List<ProjectResponseDTO>();

            var entities = (await _unitOfWork.Repository<Project>()
                    .FindAsync(p => ids.Contains(p.Id), OwnerInclude))
                .ToList();

            return await BuildListAsync(entities);
        }

        private async Task<List<ProjectResponseDTO>> BuildListAsync(List<Project> entities)
        {
            var dtos = _mapper.Map<List<ProjectResponseDTO>>(entities);
            var locations = await GetDefaultLocationsAsync(entities.Select(e => e.Id).ToList());

            for (var i = 0; i < dtos.Count; i++)
            {
                dtos[i].ProjectImageUrl = await ResolveImageUrlAsync(entities[i]);
                locations.TryGetValue(entities[i].Id, out var location);
                dtos[i].Location = location;
            }

            return dtos;
        }

        private async Task<Dictionary<Guid, ProjectLocationResponseDTO>> GetDefaultLocationsAsync(
            IReadOnlyCollection<Guid> projectIds)
        {
            var result = new Dictionary<Guid, ProjectLocationResponseDTO>();
            if (projectIds.Count == 0) return result;

            var locations = (await _unitOfWork.Repository<ProjectLocation>()
                    .FindAsync(l => projectIds.Contains(l.ProjectId)))
                .ToList();

            foreach (var group in locations.GroupBy(l => l.ProjectId))
            {
                var location = group.FirstOrDefault(l => l.IsDefault) ?? group.First();
                result[group.Key] = _mapper.Map<ProjectLocationResponseDTO>(location);
            }

            return result;
        }

        public async Task<ProjectResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = (await _unitOfWork.Repository<Project>()
                    .FindAsync(p => p.Id == id, OwnerInclude))
                .FirstOrDefault();
            if (entity == null) return null;

            var dto = _mapper.Map<ProjectResponseDTO>(entity);
            dto.ProjectImageUrl = await ResolveImageUrlAsync(entity);
            dto.Location = await GetDefaultLocationAsync(id);
            return dto;
        }

        public async Task<ProjectResponseDTO> SetImageAsync(
            Guid id, Stream content, string fileName, long sizeBytes, Guid actorId, bool isSystemAdmin,
            CancellationToken ct = default)
        {
            var entity = await _unitOfWork.Repository<Project>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Project with ID {id} not found.", 404);

            RequireAdminOrManager(entity, actorId, isSystemAdmin);

            entity.ProjectImageStoragePath = await _imageUpload.SaveImageAsync(
                content, fileName, sizeBytes, $"{ProjectImagePrefix}/{id}", ct);
            entity.ProjectImageUrl = null;
            entity.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Project>().Update(entity);

            await _auditLog.LogAsync(
                LogScope.Project, AuditAction.Update, nameof(Project), entity.Id.ToString(), actorId,
                detail: $"Cập nhật ảnh dự án '{entity.ProjectName}'", projectId: entity.Id);

            await _unitOfWork.CommitAsync();

            return await GetByIdAsync(id) ?? _mapper.Map<ProjectResponseDTO>(entity);
        }

        private static void RequireAdminOrManager(Project entity, Guid actorId, bool isSystemAdmin)
        {
            if (isSystemAdmin || entity.ManagerAccountId == actorId) return;

            throw new ApiExceptionResponse(
                "Only a system administrator or the project manager can edit this project.", 403);
        }

        private async Task<string?> ResolveImageUrlAsync(Project entity)
        {
            if (!string.IsNullOrWhiteSpace(entity.ProjectImageStoragePath))
                return await _imageUpload.GetImageUrlAsync(entity.ProjectImageStoragePath);

            return entity.ProjectImageUrl;
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
        }

        public async Task<ProjectResponseDTO> UpdateAsync(
            Guid id, UpdateProjectDTO dto, Guid actorId, bool isSystemAdmin)
        {
            var entity = await _unitOfWork.Repository<Project>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Project with ID {id} not found.", 404);

            RequireAdminOrManager(entity, actorId, isSystemAdmin);

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
