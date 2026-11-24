using AbrIT.Panel.Domain.Orders;
using OrchardCore.Locking.Distributed;
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

/// <summary><see cref="IOrderLock"/> over Orchard's tenant-wide <see cref="IDistributedLock"/>.</summary>
public sealed class DistributedOrderLock(IDistributedLock distributedLock) : IOrderLock
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

        return locker;
    }
}
