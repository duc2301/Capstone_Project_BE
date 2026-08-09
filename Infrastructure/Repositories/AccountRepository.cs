using Application.Interfaces.IRepositories;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class AccountRepository : GenericRepository<Account>, IAccountRepository
    {
        private readonly CDESystemDbContext _context;

        public AccountRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Accounts
                .AnyAsync(a => a.Email.ToLower() == email.ToLower());
        }

        public async Task<Account?> GetByEmailAsync(string email)
        {
            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower());
        }

        public async Task<HashSet<string>> GetExistingEmailsAsync(IEnumerable<string> emails)
        {
            var normalized = emails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim().ToLower())
                .Distinct()
                .ToList();

            if (normalized.Count == 0)
                return new HashSet<string>();

            var found = await _context.Accounts
                .Select(a => a.Email.ToLower())
                .Where(e => normalized.Contains(e))
                .ToListAsync();

            return found.ToHashSet();
        }
    }
}
