using Application.DTOs.RequestDTOs.FileItem;
using Application.DTOs.ResponseDTOs.FileItem;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/file-items")]
    public class FileItemsController
        : BaseCrudController<FileItem, CreateFileItemDTO, UpdateFileItemDTO, FileItemResponseDTO>
    {
        public FileItemsController(
            IGenericService<FileItem, CreateFileItemDTO, UpdateFileItemDTO, FileItemResponseDTO> service)
            : base(service) { }
    }
}
