using AbrIT.Panel.Domain.Orders;
using YesSql;
using YesSql.Services;

namespace AbrIT.Panel.Module.Orders;

/// <summary>
/// Orders as YesSql documents in the request's session. <see cref="SaveAsync"/> commits immediately, so a change
/// made inside the per-order lock is visible to the next lock holder (another request) before the lock is released
/// (DECISIONS D-12). <see cref="GetAsync"/> always returns the stored state, never a stale tracked instance.
/// </summary>
public sealed class YesSqlOrderRepository(ISession session) : IOrderRepository
{
    public Task<Order?> GetAsync(string orderId, CancellationToken ct = default) =>
        session.GetFreshAsync(() => session.Query<Order, OrderIndex>(x => x.OrderId == orderId).FirstOrDefaultAsync(ct));

    public async Task<IReadOnlyList<Order>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        var orders = await session.Query<Order, OrderIndex>(x => x.OwnerUserId == ownerUserId)
            .OrderByDescending(x => x.CreatedUtc)
            .ListAsync(ct);
        return [.. orders];
    }

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        await session.SaveAsync(order, cancellationToken: ct);
        await session.SaveChangesAsync(ct);
    }
}
