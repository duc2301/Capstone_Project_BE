using Application.DTOs.RequestDTOs.ModelFile;
using Application.DTOs.ResponseDTOs.ModelFile;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/model-files")]
    public class ModelFilesController
        : BaseCrudController<ModelFile, CreateModelFileDTO, UpdateModelFileDTO, ModelFileResponseDTO>
    {
        public ModelFilesController(
            IGenericService<ModelFile, CreateModelFileDTO, UpdateModelFileDTO, ModelFileResponseDTO> service)
            : base(service) { }
    }
}
