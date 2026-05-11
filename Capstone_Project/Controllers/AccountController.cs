using Application.DTOs.ApiResponseDTO;
using Application.DTOs.RequestDTOs.Account;
using Application.Interfaces.IServices;
using Capstone_Project.DataHandler.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/accounts")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var accounts = await _accountService.GetAllAsync();
            return Ok(ApiResponse.Success("Get all accounts successfully", accounts));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var account = await _accountService.GetByIdAsync(id);
            if (account == null)
                return NotFound(ApiResponse.Fail($"Account with ID {id} not found."));
            return Ok(ApiResponse.Success("Get account successfully", account));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAccountDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ValidationHandler.Handle(ModelState));

            var result = await _accountService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse.Success("Account created successfully", result));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ValidationHandler.Handle(ModelState));

            var result = await _accountService.UpdateAsync(id, dto);
            return Ok(ApiResponse.Success("Account updated successfully", result));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _accountService.DeleteAsync(id);
            return Ok(ApiResponse.Success("Account deleted successfully"));
        }
    }
}
