namespace Application.Interfaces.IRepositories
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(Guid? id);
        Task CreateAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        void DeleteById(Guid id);
    }
}
