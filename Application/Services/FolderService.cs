using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class FolderService : IFolderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FolderService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<FolderResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<FolderResponseDTO>>(
                await _unitOfWork.Repository<Folder>().GetAllAsync());

        public async Task<FolderResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Folder>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<FolderResponseDTO>(entity);
        }

        public async Task<FolderResponseDTO> CreateAsync(CreateFolderDTO dto)
        {
            var entity = _mapper.Map<Folder>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Folder>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderResponseDTO>(entity);
        }

        public async Task<FolderResponseDTO> UpdateAsync(Guid id, UpdateFolderDTO dto)
        {
            var entity = await _unitOfWork.Repository<Folder>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Folder with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Folder>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FolderResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Folder>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Folder with ID {id} not found.", 404);
            _unitOfWork.Repository<Folder>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
