using AbrIT.Panel.Domain.Orders;
using OrchardCore.Locking.Distributed;
using YesSql;
using IOrchardClock = OrchardCore.Modules.IClock;
using IOrchardIdGenerator = OrchardCore.Entities.IIdGenerator;

namespace AbrIT.Panel.Module.Orders;

public sealed class OrchardClock(IOrchardClock clock) : IClock
{
    public DateTime UtcNow => clock.UtcNow;
}

public sealed class OrchardIdSource(IOrchardIdGenerator generator) : IIdSource
{
    public string NewId() => generator.GenerateUniqueId();
}

/// <summary>
/// <see cref="IOrderLock"/> over Orchard's tenant-wide <see cref="IDistributedLock"/>. Scoped: right after the lock
/// is acquired the request session's current transaction is ended (<see cref="ISession.SaveChangesAsync"/>), so reads
/// inside the lock start a new transaction and see what the previous holder committed instead of an older snapshot
/// (DECISIONS D-12).
/// </summary>
public sealed class DistributedOrderLock(IDistributedLock distributedLock, ISession session) : IOrderLock
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (locker, locked) = await distributedLock.TryAcquireLockAsync($"abrit:{key}", Timeout, Expiration);
        if (!locked)
        {
            throw new TimeoutException($"Could not acquire lock '{key}' within {Timeout.TotalSeconds}s.");
        }

        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch
        {
            await locker.DisposeAsync();
            throw;
        }

        return locker;
    }
}
