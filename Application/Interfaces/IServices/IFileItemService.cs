using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFileItemService
        : IGenericService<FileItem, CreateFileItemDTO, UpdateFileItemDTO, FileItemResponseDTO>
    {
    }
}
