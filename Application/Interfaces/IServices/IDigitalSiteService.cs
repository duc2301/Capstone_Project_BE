using Application.DTOs.RequestDTOs.DigitalSite;
using Application.DTOs.ResponseDTOs.DigitalSite;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IDigitalSiteService
        : IGenericService<DigitalSite, CreateDigitalSiteDTO, UpdateDigitalSiteDTO, DigitalSiteResponseDTO>
    {
    }
}
