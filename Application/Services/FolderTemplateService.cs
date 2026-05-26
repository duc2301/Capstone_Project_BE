using Application.DTOs.RequestDTOs.FolderTemplate;
using Application.DTOs.ResponseDTOs.FolderTemplate;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class FolderTemplateService : IFolderTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FolderTemplateService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<FolderTemplateResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<FolderTemplateResponseDTO>>(
                await _unitOfWork.Repository<FolderTemplate>().GetAllAsync());

        public async Task<FolderTemplateResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<FolderTemplate>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<FolderTemplateResponseDTO>(entity);
        }

        public async Task<FolderTemplateResponseDTO> CreateAsync(CreateFolderTemplateDTO dto)
        {
            var entity = _mapper.Map<FolderTemplate>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<FolderTemplate>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderTemplateResponseDTO>(entity);
        }

        public async Task<FolderTemplateResponseDTO> UpdateAsync(Guid id, UpdateFolderTemplateDTO dto)
        {
            var entity = await _unitOfWork.Repository<FolderTemplate>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"FolderTemplate with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<FolderTemplate>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderTemplateResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<FolderTemplate>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"FolderTemplate with ID {id} not found.", 404);
            _unitOfWork.Repository<FolderTemplate>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
