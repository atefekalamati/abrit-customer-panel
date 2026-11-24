using AbrIT.Panel.Domain.Orders;
using YesSql;
using YesSql.Services;

namespace AbrIT.Panel.Module.Orders;

/// <summary>Orders as YesSql documents in the request's session (committed by Orchard at the end of the request).</summary>
public sealed class YesSqlOrderRepository(ISession session) : IOrderRepository
{
    public Task<Order?> GetAsync(string orderId, CancellationToken ct = default) =>
        session.Query<Order, OrderIndex>(x => x.OrderId == orderId).FirstOrDefaultAsync(ct)!;

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
        await session.FlushAsync(ct);
    }
}
