using System.Text.RegularExpressions;

namespace AbrIT.Panel.Domain.Orders;

public sealed partial class OrderService(
    IOrderRepository repository,
    IPlanCatalog catalog,
    IOrderNumberGenerator numbers,
    IOrderLock orderLock,
    IClock clock,
    IIdSource ids) : IOrderService
{
    public static string LockKey(string orderId) => $"order:{orderId}";

    public async Task<Order> PlaceOrderAsync(
        string userId, string userName, string planContentItemId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (string.IsNullOrEmpty(planContentItemId) || !IdPattern().IsMatch(planContentItemId))
        {
            throw new PlanNotOrderableException(planContentItemId ?? string.Empty);
        }

        var plan = await catalog.GetAsync(planContentItemId, ct);
        if (plan is not { IsOrderable: true })
        {
            throw new PlanNotOrderableException(planContentItemId);
        }

        var order = new Order
        {
            OrderId = ids.NewId(),
            OrderNumber = await numbers.NextAsync(ct),
            OwnerUserId = userId,
            OwnerUserName = userName ?? string.Empty,
            PlanContentItemId = planContentItemId,
            PlanSnapshot = plan.Snapshot,
            AmountToman = plan.Snapshot.PriceToman,
            Status = OrderStatus.PendingPayment,
            PaymentStatus = PaymentStatus.Pending,
            CreatedUtc = clock.UtcNow,
        };

        await repository.SaveAsync(order, ct);
        return order;
    }

    public async Task<Order?> GetForOwnerAsync(string userId, string orderId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(orderId) || !IdPattern().IsMatch(orderId))
        {
            return null;
        }

        var order = await repository.GetAsync(orderId, ct);
        return order is not null && order.OwnerUserId == userId ? order : null;
    }

    public Task<IReadOnlyList<Order>> ListForOwnerAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return repository.ListByOwnerAsync(userId, ct);
    }

    public async Task<Order?> CancelAsync(string userId, string orderId, CancellationToken ct = default)
    {
        if (await GetForOwnerAsync(userId, orderId, ct) is null)
        {
            return null;
        }

        await using (await orderLock.AcquireAsync(LockKey(orderId), ct))
        {
            // Reload inside the lock: status may have changed (e.g. admin approved) since the check above.
            var order = await GetForOwnerAsync(userId, orderId, ct);
            if (order is null)
            {
                return null;
            }

            order.Apply(OrderTrigger.Cancel, userId, clock.UtcNow);
            await repository.SaveAsync(order, ct);
            return order;
        }
    }

    /// <summary>Content item ids and generated ids: letters, digits, '-' and '_' only.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdPattern();
}
