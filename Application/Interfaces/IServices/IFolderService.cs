using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFolderService
        : IGenericService<Folder, CreateFolderDTO, UpdateFolderDTO, FolderResponseDTO>
    {
    }
}
