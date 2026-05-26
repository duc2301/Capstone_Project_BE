using Application.DTOs.RequestDTOs.FolderTemplate;
using Application.DTOs.ResponseDTOs.FolderTemplate;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IFolderTemplateService
        : IGenericService<FolderTemplate, CreateFolderTemplateDTO, UpdateFolderTemplateDTO, FolderTemplateResponseDTO>
    {
    }
}
