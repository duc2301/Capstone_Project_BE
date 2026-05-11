using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    public interface IAccountRepository : IGenericRepository<Account>
    {
        Task<Account?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
    }
}
