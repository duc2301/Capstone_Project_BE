using Application.DTOs.RequestDTOs.Submittal;
using Application.DTOs.ResponseDTOs.Submittal;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface ISubmittalService
        : IGenericService<Submittal, CreateSubmittalDTO, UpdateSubmittalDTO, SubmittalResponseDTO>
    {
    }
}
