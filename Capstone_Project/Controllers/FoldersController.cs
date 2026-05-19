using Application.DTOs.RequestDTOs.Folder;
using Application.DTOs.ResponseDTOs.Folder;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/folders")]
    public class FoldersController
        : BaseCrudController<Folder, CreateFolderDTO, UpdateFolderDTO, FolderResponseDTO>
    {
        public FoldersController(
            IGenericService<Folder, CreateFolderDTO, UpdateFolderDTO, FolderResponseDTO> service)
            : base(service) { }
    }
}
