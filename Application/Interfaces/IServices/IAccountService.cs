using Application.DTOs.RequestDTOs.Account;
using Application.DTOs.ResponseDTOs.Account;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    // CRUD chuẩn kế thừa từ IGenericService; có thể bổ sung method riêng (vd: GetByEmail) ở đây sau này.
    public interface IAccountService
        : IGenericService<Account, CreateAccountDTO, UpdateAccountDTO, AccountResponseDTO>
    {
    }
}
