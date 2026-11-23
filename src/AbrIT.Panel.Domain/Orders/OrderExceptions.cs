namespace AbrIT.Panel.Domain.Orders;

public sealed class InvalidOrderTransitionException(OrderStatus from, OrderTrigger trigger)
    : InvalidOperationException($"Order transition '{trigger}' is not allowed from status '{from}'.")
{
    public OrderStatus From { get; } = from;

    public OrderTrigger Trigger { get; } = trigger;
}

/// <summary>Thrown for unknown plan ids and for plans that are not orderable (same error on purpose, see A-14).</summary>
public sealed class PlanNotOrderableException(string planContentItemId)
    : InvalidOperationException("The requested plan does not exist or cannot be ordered.")
{
    public string PlanContentItemId { get; } = planContentItemId;
}
