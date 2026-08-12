using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Loi;
using Application.Interfaces.IServices;
using Capstone_Project.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [ApiController]
    [Route("api/projects")]
    [Authorize]
    public class ProjectLoiRuleSetController : ControllerBase
    {
        private readonly ILoiRuleAdminService _rules;

        public ProjectLoiRuleSetController(ILoiRuleAdminService rules)
        {
            _rules = rules;
        }

        [HttpGet("{projectId:guid}/loi-rule-set")]
        public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
            => Ok(ApiResponse.Success("Project LOI rule set retrieved",
                await _rules.GetProjectRuleSetAsync(projectId, ct)));

        [HttpGet("{projectId:guid}/loi-rule-sets")]
        public async Task<IActionResult> GetSelectable(Guid projectId, CancellationToken ct)
            => Ok(ApiResponse.Success("Selectable LOI rule sets retrieved",
                await _rules.GetSelectableRuleSetsAsync(
                    projectId, User.GetAccountId(), User.IsAdmin(), ct)));

        [HttpPut("{projectId:guid}/loi-rule-set")]
        public async Task<IActionResult> Set(
            Guid projectId, [FromBody] SetProjectLoiRuleSetDTO dto, CancellationToken ct)
            => Ok(ApiResponse.Success("Project LOI rule set updated",
                await _rules.SetProjectRuleSetAsync(
                    projectId, dto.RuleSetId, User.GetAccountId(), User.IsAdmin(), ct)));
    }
}
