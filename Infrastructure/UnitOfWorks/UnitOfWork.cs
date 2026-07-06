using Application.Interfaces.IRepositories;
using Application.Interfaces.IUnitOfWork;
using Infrastructure.DbContexts;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.UnitOfWorks
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly CDESystemDbContext _context;
        private IDbContextTransaction? _transaction;
        private bool _disposed;

        public UnitOfWork(CDESystemDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        private IAccountRepository? _accountRepository;
        public IAccountRepository AccountRepository =>
            _accountRepository ??= new AccountRepository(_context);

        private IRefreshTokenRepository? _refreshTokenRepository;
        public IRefreshTokenRepository RefreshTokenRepository =>
            _refreshTokenRepository ??= new RefreshTokenRepository(_context);

        private IFilePermissionRepository? _filePermissionRepository;
        public IFilePermissionRepository FilePermissionRepository =>
            _filePermissionRepository ??= new FilePermissionRepository(_context);

        private IFolderPermissionRepository? _folderPermissionRepository;
        public IFolderPermissionRepository FolderPermissionRepository =>
            _folderPermissionRepository ??= new FolderPermissionRepository(_context);

        private IDocumentSearchRepository? _documentSearchRepository;
        public IDocumentSearchRepository DocumentSearchRepository =>
            _documentSearchRepository ??= new DocumentSearchRepository(_context);

        private readonly Dictionary<Type, object> _repositories = new();
        public IGenericRepository<T> Repository<T>() where T : class
        {
            if (_repositories.TryGetValue(typeof(T), out var existing))
                return (IGenericRepository<T>)existing;

            var repository = new GenericRepository<T>(_context);
            _repositories[typeof(T)] = repository;
            return repository;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task CommitAsync()
        {
            try
            {
                await _context.Database.BeginTransactionAsync();
                await _context.SaveChangesAsync();
                await _context.Database.CommitTransactionAsync();
            }
            catch
            {
                await RollbackAsync();
                throw;
            }
        }

        private async Task RollbackAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _transaction?.Dispose();
                    _context.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
