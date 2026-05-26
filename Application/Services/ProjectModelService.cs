using Application.DTOs.RequestDTOs.ProjectModel;
using Application.DTOs.ResponseDTOs.ProjectModel;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class ProjectModelService : IProjectModelService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ProjectModelService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ProjectModelResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ProjectModelResponseDTO>>(
                await _unitOfWork.Repository<ProjectModel>().GetAllAsync());

        public async Task<ProjectModelResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ProjectModel>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<ProjectModelResponseDTO>(entity);
        }

        public async Task<ProjectModelResponseDTO> CreateAsync(CreateProjectModelDTO dto)
        {
            var entity = _mapper.Map<ProjectModel>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<ProjectModel>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ProjectModelResponseDTO>(entity);
        }

        public async Task<ProjectModelResponseDTO> UpdateAsync(Guid id, UpdateProjectModelDTO dto)
        {
            var entity = await _unitOfWork.Repository<ProjectModel>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ProjectModel with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<ProjectModel>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ProjectModelResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ProjectModel>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ProjectModel with ID {id} not found.", 404);
            _unitOfWork.Repository<ProjectModel>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
