using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    public interface IAccountRepository : IGenericRepository<Account>
    {
        Task<Account?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);

        // Trả về tập email (đã lowercase) đang tồn tại trong DB — dùng cho import hàng loạt,
        // tránh gọi EmailExistsAsync N lần.
        Task<HashSet<string>> GetExistingEmailsAsync(IEnumerable<string> emails);
    }
}
