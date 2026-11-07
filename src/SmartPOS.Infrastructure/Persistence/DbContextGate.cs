namespace SmartPOS.Infrastructure.Persistence;

/// <summary>
/// Serializes EF operations per Blazor circuit so a single scoped <see cref="AppDbContext"/>
/// is never used concurrently (layout + page init often overlap).
/// </summary>
public sealed class DbContextGate(AppDbContext db)
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await operation();
        }
        finally
        {
            try
            {
                db.ChangeTracker.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await operation();
        }
        finally
        {
            try
            {
                db.ChangeTracker.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
