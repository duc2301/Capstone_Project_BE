using Application.DTOs.RequestDTOs.Project;
using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IFolderBootstrapService _folderBootstrap;

        public ProjectService(IUnitOfWork unitOfWork, IMapper mapper, IFolderBootstrapService folderBootstrap)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _folderBootstrap = folderBootstrap;
        }

        public async Task<IEnumerable<ProjectResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ProjectResponseDTO>>(
                await _unitOfWork.Repository<Project>().GetAllAsync());

        public async Task<ProjectResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Project>().GetByIdAsync(id);
            if (entity == null) return null;

            var dto = _mapper.Map<ProjectResponseDTO>(entity);
            dto.Location = await GetDefaultLocationAsync(id);
            return dto;
        }

        public async Task<ProjectResponseDTO> CreateAsync(CreateProjectDTO dto)
        {
            var entity = _mapper.Map<Project>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
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

            await _unitOfWork.CommitAsync();

            // Dựng 4 khu vực CDE gốc (WIP/Shared/Published/Archived) ngay khi tạo dự án.
            await _folderBootstrap.InitializeRootFoldersAsync(entity.Id);

            var result = _mapper.Map<ProjectResponseDTO>(entity);
            result.Location = await GetDefaultLocationAsync(entity.Id);
            return result;
        }

        private async Task<ProjectLocationResponseDTO?> GetDefaultLocationAsync(Guid projectId)
        {
            var locations = (await _unitOfWork.Repository<ProjectLocation>()
                    .FindAsync(l => l.ProjectId == projectId))
                .ToList();
            var location = locations.FirstOrDefault(l => l.IsDefault) ?? locations.FirstOrDefault();
            return location == null ? null : _mapper.Map<ProjectLocationResponseDTO>(location);
        }

        public async Task<ProjectResponseDTO> UpdateAsync(Guid id, UpdateProjectDTO dto)
        {
            var entity = await _unitOfWork.Repository<Project>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Project with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Project>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ProjectResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Project>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Project with ID {id} not found.", 404);
            _unitOfWork.Repository<Project>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
