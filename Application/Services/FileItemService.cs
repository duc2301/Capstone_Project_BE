using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class FileItemService : IFileItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FileItemService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<FileItemResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<FileItemResponseDTO>>(
                await _unitOfWork.Repository<FileItem>().GetAllAsync());

        public async Task<FileItemResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<FileItem>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<FileItemResponseDTO>(entity);
        }

        public async Task<FileItemResponseDTO> CreateAsync(CreateFileItemDTO dto)
        {
            var entity = _mapper.Map<FileItem>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<FileItem>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FileItemResponseDTO>(entity);
        }

        public async Task<FileItemResponseDTO> UpdateAsync(Guid id, UpdateFileItemDTO dto)
        {
            var entity = await _unitOfWork.Repository<FileItem>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"FileItem with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<FileItem>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<FileItemResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<FileItem>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"FileItem with ID {id} not found.", 404);
            _unitOfWork.Repository<FileItem>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
