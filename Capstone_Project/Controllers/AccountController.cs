using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Account;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/accounts")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAccountDTO dto)
        {

            var result = await _accountService.CreateAsync(dto);
            return Ok(ApiResponse.Success("Registration successful", result));
        }

    }
}
