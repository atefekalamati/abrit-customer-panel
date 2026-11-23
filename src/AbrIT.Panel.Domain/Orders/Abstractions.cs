namespace AbrIT.Panel.Domain.Orders;

public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>Generates unique document ids (module: Orchard <c>IIdGenerator</c>).</summary>
public interface IIdSource
{
    string NewId();
}

/// <summary>Named async lock (module: Orchard <c>IDistributedLock</c>). Dispose the result to release.</summary>
public interface IOrderLock
{
    Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct = default);
}

public interface IOrderRepository
{
    Task<Order?> GetAsync(string orderId, CancellationToken ct = default);

    /// <summary>Orders of one owner, newest first.</summary>
    Task<IReadOnlyList<Order>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default);

    Task SaveAsync(Order order, CancellationToken ct = default);
}

/// <summary>A plan as seen by the order flow: what would be snapshotted and whether it may be ordered now.</summary>
public sealed record PlanInfo(PlanSnapshot Snapshot, bool IsOrderable);

public interface IPlanCatalog
{
    /// <summary>Returns <c>null</c> when no published plan has this content item id.</summary>
    Task<PlanInfo?> GetAsync(string planContentItemId, CancellationToken ct = default);
}

public interface IOrderNumberGenerator
{
    /// <summary>Next unique human-readable number, e.g. <c>ABR-1405-000124</c>.</summary>
    Task<string> NextAsync(CancellationToken ct = default);
}

/// <summary>Persistent counters. Callers serialize access per key with <see cref="IOrderLock"/>.</summary>
public interface ISequenceStore
{
    /// <summary>Increments the counter for <paramref name="key"/> (starting at 0), persists it, and returns the new value.</summary>
    Task<long> IncrementAsync(string key, CancellationToken ct = default);
}

public interface IOrderService
{
    /// <exception cref="PlanNotOrderableException">Plan id is malformed, unknown, or not orderable.</exception>
    Task<Order> PlaceOrderAsync(string userId, string userName, string planContentItemId, CancellationToken ct = default);

    /// <summary>Returns <c>null</c> when the order does not exist or belongs to someone else (callers answer 404).</summary>
    Task<Order?> GetForOwnerAsync(string userId, string orderId, CancellationToken ct = default);

    Task<IReadOnlyList<Order>> ListForOwnerAsync(string userId, CancellationToken ct = default);

    /// <summary>Cancels the caller's own order. Returns <c>null</c> when not found / not owned.</summary>
    /// <exception cref="InvalidOrderTransitionException">The order is no longer <see cref="OrderStatus.PendingPayment"/>.</exception>
    Task<Order?> CancelAsync(string userId, string orderId, CancellationToken ct = default);
}
