using Application.DTOs.RequestDTOs.LandParcel;
using Application.DTOs.ResponseDTOs.LandParcel;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface ILandParcelService
        : IGenericService<LandParcel, CreateLandParcelDTO, UpdateLandParcelDTO, LandParcelResponseDTO>
    {
    }
}
