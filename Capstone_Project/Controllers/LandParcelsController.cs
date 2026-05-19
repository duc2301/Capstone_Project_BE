using Application.DTOs.RequestDTOs.LandParcel;
using Application.DTOs.ResponseDTOs.LandParcel;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/land-parcels")]
    public class LandParcelsController
        : BaseCrudController<LandParcel, CreateLandParcelDTO, UpdateLandParcelDTO, LandParcelResponseDTO>
    {
        public LandParcelsController(
            IGenericService<LandParcel, CreateLandParcelDTO, UpdateLandParcelDTO, LandParcelResponseDTO> service)
            : base(service) { }
    }
}
