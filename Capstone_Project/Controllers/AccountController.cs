using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Account;
using Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateAccountDTO dto)
        {

            var result = await _accountService.CreateAsync(dto);
            return Ok(ApiResponse.Success("Registration successful", result));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _accountService.GetAllAsync();
            return Ok(ApiResponse.Success("Accounts retrieved successfully", result));

        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountDTO dto)
        {
            var result = await _accountService.UpdateAsync(id, dto);
            return Ok(ApiResponse.Success("Account updated successfully", result));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _accountService.DeleteAsync(id);
            return Ok(ApiResponse.Success("Account deleted successfully"));

        }
    }
}
