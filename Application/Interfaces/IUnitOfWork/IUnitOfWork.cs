using Application.Interfaces.IRepositories;

namespace Application.Interfaces.IUnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IAccountRepository AccountRepository { get; }

        Task<int> SaveChangesAsync();
        Task CommitAsync();
    }
}
