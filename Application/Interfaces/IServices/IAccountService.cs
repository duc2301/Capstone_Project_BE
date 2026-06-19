using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.ResponseDTOs.Account;

namespace Application.Interfaces.IServices
{
    public interface IAccountService
    {
        Task<IEnumerable<AccountResponseDTO>> GetAllAsync();
        Task<AccountResponseDTO?> GetByIdAsync(Guid id);
        Task<AccountResponseDTO> CreateAsync(CreateAccountDTO dto);
        Task<AccountResponseDTO> UpdateAsync(Guid id, UpdateAccountDTO dto);
        Task DeleteAsync(Guid id);
    }
}
