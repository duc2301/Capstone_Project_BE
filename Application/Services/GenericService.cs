using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;

namespace Application.Services
{
    // CRUD chuẩn cho mọi aggregate root. Entity có rule riêng thì kế thừa & override.
    public class GenericService<TEntity, TCreate, TUpdate, TResponse>
        : IGenericService<TEntity, TCreate, TUpdate, TResponse>
        where TEntity : class, IEntity
    {
        protected readonly IUnitOfWork UnitOfWork;
        protected readonly IMapper Mapper;

        public GenericService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            UnitOfWork = unitOfWork;
            Mapper = mapper;
        }

        public virtual async Task<IEnumerable<TResponse>> GetAllAsync()
        {
            var items = await UnitOfWork.Repository<TEntity>().GetAllAsync();
            return Mapper.Map<IEnumerable<TResponse>>(items);
        }

        public virtual async Task<TResponse?> GetByIdAsync(Guid id)
        {
            var entity = await UnitOfWork.Repository<TEntity>().GetByIdAsync(id);
            return entity is null ? default : Mapper.Map<TResponse>(entity);
        }

        public virtual async Task<TResponse> CreateAsync(TCreate dto)
        {
            var entity = Mapper.Map<TEntity>(dto);
            entity.Id = Guid.NewGuid();

            if (entity is IAuditable auditable)
            {
                var now = DateTime.UtcNow;
                auditable.CreatedAt = now;
                auditable.UpdatedAt = now;
            }

            await UnitOfWork.Repository<TEntity>().CreateAsync(entity);
            await UnitOfWork.CommitAsync();

            return Mapper.Map<TResponse>(entity);
        }

        public virtual async Task<TResponse> UpdateAsync(Guid id, TUpdate dto)
        {
            var entity = await UnitOfWork.Repository<TEntity>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"{typeof(TEntity).Name} with ID {id} not found.", 404);

            Mapper.Map(dto, entity);

            if (entity is IAuditable auditable)
                auditable.UpdatedAt = DateTime.UtcNow;

            UnitOfWork.Repository<TEntity>().Update(entity);
            await UnitOfWork.CommitAsync();

            return Mapper.Map<TResponse>(entity);
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            var entity = await UnitOfWork.Repository<TEntity>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"{typeof(TEntity).Name} with ID {id} not found.", 404);

            UnitOfWork.Repository<TEntity>().Delete(entity);
            await UnitOfWork.CommitAsync();
        }
    }
}
