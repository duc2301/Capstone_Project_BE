using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Issue;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/issues")]
    public class IssuesController
        : BaseCrudController<Issue, CreateIssueDTO, UpdateIssueDTO, IssueResponseDTO>
    {
        public IssuesController(
            IGenericService<Issue, CreateIssueDTO, UpdateIssueDTO, IssueResponseDTO> service)
            : base(service) { }
    }
}
