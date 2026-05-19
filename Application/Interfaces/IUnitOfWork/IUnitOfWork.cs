using Application.Interfaces.IRepositories;

namespace Application.Interfaces.IUnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IAccountRepository AccountRepository { get; }
        IRefreshTokenRepository RefreshTokenRepository { get; }

        // Repo generic dùng chung cho mọi entity (không cần khai báo riêng từng cái)
        IGenericRepository<T> Repository<T>() where T : class;

        Task<int> SaveChangesAsync();
        Task CommitAsync();
    }
}
