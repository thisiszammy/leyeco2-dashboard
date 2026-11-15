using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leyeco2Dashboard.Data
{
    // One context today. The API stays generic over the DbContext, as the HRIS's does, so a
    // second database (the transaction sources the dashboard will read) joins without touching
    // a service: each context begins and commits on its own, there is no distributed transaction.
    public class UnitOfWork : IUnitOfWork
    {
        private IDbContextTransaction? _databaseTransaction;

        public UnitOfWork(DatabaseContext databaseContext)
        {
            DatabaseContext = databaseContext;
        }

        public DatabaseContext DatabaseContext { get; }

        public async Task BeginTransactionAsync<TContext>() where TContext : DbContext
        {
            if (typeof(TContext) == typeof(DatabaseContext) && _databaseTransaction == null)
            {
                _databaseTransaction = await DatabaseContext.Database.BeginTransactionAsync();
            }
        }

        public async Task CommitAsync<TContext>() where TContext : DbContext
        {
            if (typeof(TContext) == typeof(DatabaseContext) && _databaseTransaction != null)
            {
                await DatabaseContext.SaveChangesAsync();
                await _databaseTransaction.CommitAsync();
                await _databaseTransaction.DisposeAsync();
                _databaseTransaction = null;
            }
        }

        public async Task RollbackAsync<TContext>() where TContext : DbContext
        {
            if (typeof(TContext) == typeof(DatabaseContext) && _databaseTransaction != null)
            {
                await _databaseTransaction.RollbackAsync();
                await _databaseTransaction.DisposeAsync();
                _databaseTransaction = null;
            }
        }

        public Task SaveChangesAsync<TContext>(CancellationToken ct = default) where TContext : DbContext
        {
            if (typeof(TContext) == typeof(DatabaseContext))
            {
                return DatabaseContext.SaveChangesAsync(ct);
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _databaseTransaction?.Dispose();
            DatabaseContext.Dispose();
        }
    }
}
