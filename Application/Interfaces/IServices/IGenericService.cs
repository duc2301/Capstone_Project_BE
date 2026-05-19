namespace Application.Interfaces.IServices
{
    // Hợp đồng CRUD chung. TEntity nằm trong type param để đăng ký DI open-generic.
    public interface IGenericService<TEntity, TCreate, TUpdate, TResponse>
    {
        Task<IEnumerable<TResponse>> GetAllAsync();
        Task<TResponse?> GetByIdAsync(Guid id);
        Task<TResponse> CreateAsync(TCreate dto);
        Task<TResponse> UpdateAsync(Guid id, TUpdate dto);
        Task DeleteAsync(Guid id);
    }
}
