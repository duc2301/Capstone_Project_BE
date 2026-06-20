using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Services
{
    public class FileItemService : IFileItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderPermissionService _permission;
        private readonly IMapper _mapper;

        public FileItemService(
            IUnitOfWork unitOfWork,
            IFolderPermissionService permission,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _permission = permission;
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

        // Danh sách file trong 1 folder (gộp version hiện hành + tác giả). Gate quyền View.
        public async Task<IEnumerable<FileListItemDTO>> GetByFolderAsync(Guid folderId, Guid actorId)
        {
            _ = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            await _permission.RequireAsync(actorId, folderId, FolderAction.View);

            var files = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => f.FolderId == folderId))
                .ToList();
            if (files.Count == 0) return Enumerable.Empty<FileListItemDTO>();

            var fileIds = files.Select(f => f.Id).ToList();
            var versionsById = (await _unitOfWork.Repository<FileVersion>()
                    .FindAsync(v => fileIds.Contains(v.FileItemId)))
                .ToDictionary(v => v.Id);
            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return files.Select(f =>
            {
                FileVersion? cur = f.CurrentVersionId.HasValue && versionsById.TryGetValue(f.CurrentVersionId.Value, out var v) ? v : null;
                return new FileListItemDTO
                {
                    Id = f.Id,
                    FolderId = f.FolderId,
                    Name = f.Name,
                    FileType = f.FileType,
                    Status = f.Status,
                    CurrentVersionId = f.CurrentVersionId,
                    CurrentVersionNumber = cur?.VersionNumber ?? 0,
                    SizeBytes = cur?.FileSizeBytes ?? 0,
                    Format = cur?.Format,
                    CreatedByAccountId = f.CreatedByAccountId,
                    AuthorName = f.CreatedByAccountId.HasValue && accounts.TryGetValue(f.CreatedByAccountId.Value, out var a) ? a.UserName : null,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt,
                };
            }).ToList();
        }

        // Tất cả phiên bản của 1 file (mới nhất trước). Gate quyền View trên folder của file.
        public async Task<IEnumerable<FileVersionResponseDTO>> GetVersionsAsync(Guid fileItemId, Guid actorId)
        {
            var file = await _unitOfWork.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            await _permission.RequireAsync(actorId, file.FolderId, FolderAction.View);

            var accounts = (await _unitOfWork.Repository<Account>().GetAllAsync())
                .ToDictionary(a => a.Id);

            return (await _unitOfWork.Repository<FileVersion>()
                    .FindAsync(v => v.FileItemId == fileItemId))
                .OrderByDescending(v => v.VersionNumber)
                .Select(v =>
                {
                    var dto = _mapper.Map<FileVersionResponseDTO>(v);
                    dto.UploadedByName = v.UploadedByAccountId.HasValue && accounts.TryGetValue(v.UploadedByAccountId.Value, out var a) ? a.UserName : null;
                    return dto;
                })
                .ToList();
        }
    }
}
