namespace AbrIT.Panel.Domain.Orders;

public interface IAdminOrderService
{
    /// <summary>Admin cancels an unpaid order (DECISIONS A-03).</summary>
    /// <exception cref="OperatorNotAuthorizedException">Actor lacks <see cref="OperatorAction.CancelOrder"/>.</exception>
    /// <exception cref="OrderNotFoundException">No such order.</exception>
    /// <exception cref="InvalidOrderTransitionException">The order is no longer <see cref="OrderStatus.PendingPayment"/>.</exception>
    Task<Order> CancelAsync(string orderId, string adminUserId, CancellationToken ct = default);
}

public sealed class AdminOrderService(
    IOrderRepository orders,
    IOrderLock orderLock,
    IOperatorAuthorizer authorizer,
    IClock clock) : IAdminOrderService
{
    public async Task<Order> CancelAsync(string orderId, string adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(adminUserId)
            || !await authorizer.IsAuthorizedAsync(adminUserId, OperatorAction.CancelOrder, ct))
        {
            throw new OperatorNotAuthorizedException(OperatorAction.CancelOrder);
        }

        await using (await orderLock.AcquireAsync(OrderService.LockKey(orderId), ct))
        {
            var order = await orders.GetAsync(orderId, ct) ?? throw new OrderNotFoundException(orderId);
            order.Apply(OrderTrigger.Cancel, adminUserId, clock.UtcNow, "Cancelled by admin");
            await orders.SaveAsync(order, ct);
            return order;
        }
    }
}
