using Microsoft.EntityFrameworkCore;

namespace Leyeco2Dashboard.Data.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        Task BeginTransactionAsync<TContext>() where TContext : DbContext;
        Task CommitAsync<TContext>() where TContext : DbContext;
        Task RollbackAsync<TContext>() where TContext : DbContext;
        Task SaveChangesAsync<TContext>(CancellationToken ct = default) where TContext : DbContext;
    }
}
