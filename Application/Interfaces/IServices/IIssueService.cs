using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Issue;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IIssueService
        : IGenericService<Issue, CreateIssueDTO, UpdateIssueDTO, IssueResponseDTO>
    {
    }
}
