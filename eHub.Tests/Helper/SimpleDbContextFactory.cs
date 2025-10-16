using Microsoft.EntityFrameworkCore;

namespace eHub.Tests.Helper;

internal class SimpleDbContextFactory<TContext>(DbContextOptions<TContext> options) : IDbContextFactory<TContext> where TContext : DbContext
{
    private readonly DbContextOptions<TContext> _options = options;

    public TContext CreateDbContext()
    {
        return (TContext)Activator.CreateInstance(typeof(TContext), _options)!;
    }

    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
