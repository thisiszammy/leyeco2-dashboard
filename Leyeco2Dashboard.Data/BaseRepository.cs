using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data
{
    public class BaseRepository<T> where T : DbContext
    {
        protected UnitOfWork UnitOfWork { get; set; }

        protected DbContext Context => UnitOfWork.DatabaseContext;

        public BaseRepository(UnitOfWork unitOfWork)
        {
            if (unitOfWork == null) throw new ArgumentNullException(nameof(unitOfWork));
            UnitOfWork = unitOfWork;
        }

        protected virtual DbSet<TEntity> GetDbSet<TEntity>() where TEntity : class
        {
            return Context.Set<TEntity>();
        }

        protected virtual void SetEntityState(object entity, EntityState entityState)
        {
            Context.Entry(entity).State = entityState;
        }
    }
}
