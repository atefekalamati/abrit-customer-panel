using AbrIT.Panel.Domain.Provisioning;

namespace AbrIT.Panel.Domain.Orders;

public interface IPaymentApprovalService
{
    /// <summary>
    /// Admin confirms the simulated payment, then provisioning runs synchronously (DECISIONS A-01).
    /// Idempotent: an already paid order is returned unchanged (provisioning is resumed only if still <c>Paid</c>).
    /// </summary>
    /// <exception cref="OperatorNotAuthorizedException">Actor lacks <see cref="OperatorAction.ApprovePayment"/>.</exception>
    /// <exception cref="OrderNotFoundException">No such order.</exception>
    /// <exception cref="InvalidOrderTransitionException">The order is cancelled.</exception>
    Task<Order> ApproveAsync(string orderId, string adminUserId, CancellationToken ct = default);

    /// <summary>Same path as <see cref="ApproveAsync"/>, for a callback whose signature the caller has verified.</summary>
    Task<Order> ApproveFromVerifiedCallbackAsync(string orderId, CancellationToken ct = default);
}

public sealed class PaymentApprovalService(
    IOrderRepository orders,
    IOrderLock orderLock,
    IOperatorAuthorizer authorizer,
    IProvisioningService provisioning,
    IClock clock) : IPaymentApprovalService
{
    public async Task<Order> ApproveAsync(string orderId, string adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(adminUserId)
            || !await authorizer.IsAuthorizedAsync(adminUserId, OperatorAction.ApprovePayment, ct))
        {
            throw new OperatorNotAuthorizedException(OperatorAction.ApprovePayment);
        }

        return await ApproveCoreAsync(orderId, adminUserId, ct);
    }

    public Task<Order> ApproveFromVerifiedCallbackAsync(string orderId, CancellationToken ct = default) =>
        ApproveCoreAsync(orderId, SystemActors.PaymentCallback, ct);

    private async Task<Order> ApproveCoreAsync(string orderId, string actorId, CancellationToken ct)
    {
        Order order;
        await using (await orderLock.AcquireAsync(OrderService.LockKey(orderId), ct))
        {
            order = await orders.GetAsync(orderId, ct) ?? throw new OrderNotFoundException(orderId);

            // Payment already confirmed but provisioning failed: approving again changes nothing (retry is separate).
            if (order.Status == OrderStatus.Failed && order.PaymentStatus == PaymentStatus.ConfirmedSimulated)
            {
                return order;
            }

            var before = order.Status;
            order.Apply(OrderTrigger.Approve, actorId, clock.UtcNow, "Simulated payment confirmed");
            if (order.Status != before)
            {
                await orders.SaveAsync(order, ct);
            }
        }

        // Outside the approve lock: provisioning takes the same per-order lock itself.
        return order.Status == OrderStatus.Paid
            ? (await provisioning.ProvisionAsync(orderId, ct)).Order
            : order;
    }
}
