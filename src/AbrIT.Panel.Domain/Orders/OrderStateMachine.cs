namespace AbrIT.Panel.Domain.Orders;

/// <summary>
/// Pure order state machine (AGENTS.md §4.3). Any transition outside the graph throws
/// <see cref="InvalidOrderTransitionException"/>; approving an already paid/provisioning/active
/// order is an idempotent no-op that returns the current state.
/// </summary>
public static class OrderStateMachine
{
    public static OrderStatus Next(OrderStatus from, OrderTrigger trigger) =>
        TryNext(from, trigger) ?? throw new InvalidOrderTransitionException(from, trigger);

    public static bool CanApply(OrderStatus from, OrderTrigger trigger) => TryNext(from, trigger) is not null;

    private static OrderStatus? TryNext(OrderStatus from, OrderTrigger trigger) => (from, trigger) switch
    {
        (OrderStatus.PendingPayment, OrderTrigger.Approve) => OrderStatus.Paid,
        (OrderStatus.PendingPayment, OrderTrigger.Cancel) => OrderStatus.Cancelled,
        (OrderStatus.Paid, OrderTrigger.Start) => OrderStatus.Provisioning,
        (OrderStatus.Provisioning, OrderTrigger.Succeed) => OrderStatus.Active,
        (OrderStatus.Provisioning, OrderTrigger.Fail) => OrderStatus.Failed,
        (OrderStatus.Failed, OrderTrigger.Retry) => OrderStatus.Provisioning,
        (OrderStatus.Paid or OrderStatus.Provisioning or OrderStatus.Active, OrderTrigger.Approve) => from,
        _ => null,
    };
}
