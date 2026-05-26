using Application.DTOs.RequestDTOs.ModelFile;
using Application.DTOs.ResponseDTOs.ModelFile;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IModelFileService
        : IGenericService<ModelFile, CreateModelFileDTO, UpdateModelFileDTO, ModelFileResponseDTO>
    {
    }
}
