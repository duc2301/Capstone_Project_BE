using Application.Interfaces.IRepositories;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
    {
        private readonly CDESystemDbContext _context;

        public RefreshTokenRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        // Trả về bản ghi đang được EF theo dõi để mutate (revoke) rồi SaveChanges.
        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }
    }
}
