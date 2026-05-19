using Application.DTOs.RequestDTOs.FolderTemplate;
using Application.DTOs.ResponseDTOs.FolderTemplate;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/folder-templates")]
    public class FolderTemplatesController
        : BaseCrudController<FolderTemplate, CreateFolderTemplateDTO, UpdateFolderTemplateDTO, FolderTemplateResponseDTO>
    {
        public FolderTemplatesController(
            IGenericService<FolderTemplate, CreateFolderTemplateDTO, UpdateFolderTemplateDTO, FolderTemplateResponseDTO> service)
            : base(service) { }
    }
}
