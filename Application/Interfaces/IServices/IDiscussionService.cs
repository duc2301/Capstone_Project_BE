using Application.DTOs.RequestDTOs.Discussion;
using Application.DTOs.ResponseDTOs.Discussion;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IDiscussionService
        : IGenericService<Discussion, CreateDiscussionDTO, UpdateDiscussionDTO, DiscussionResponseDTO>
    {
    }
}
