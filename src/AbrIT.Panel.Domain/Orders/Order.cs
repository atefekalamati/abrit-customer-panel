namespace AbrIT.Panel.Domain.Orders;

/// <summary>Price/quota of the plan frozen at order time (A-05).</summary>
public sealed record PlanSnapshot(string PlanKey, string Title, long PriceToman, int QuotaPerUserMB, int MaxUsers);

public sealed record StatusChange(OrderStatus From, OrderStatus To, DateTime AtUtc, string ActorUserId, string? Note);

/// <summary>
/// A customer order, persisted as a YesSql document. Properties are settable for serialization;
/// status must only change through <see cref="Apply"/>.
/// </summary>
public sealed class Order
{
    /// <summary>
    /// Storage document id (YesSql sets it on save/load). Lets an instance that is no longer tracked by the session
    /// (e.g. after a commit) update its own document instead of inserting a copy (DECISIONS D-12).
    /// </summary>
    public long Id { get; set; }

    public string OrderId { get; set; } = string.Empty;

    public string OrderNumber { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string OwnerUserName { get; set; } = string.Empty;

    public string PlanContentItemId { get; set; } = string.Empty;

    public PlanSnapshot PlanSnapshot { get; set; } = new(string.Empty, string.Empty, 0, 0, 0);

    public long AmountToman { get; set; }

    public OrderStatus Status { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? PaidUtc { get; set; }

    public string? PaidByUserId { get; set; }

    public DateTime? CancelledUtc { get; set; }

    public DateTime? ActivatedUtc { get; set; }

    public List<StatusChange> History { get; set; } = [];

    /// <summary>
    /// Applies <paramref name="trigger"/> via <see cref="OrderStateMachine"/>. Records history and timestamps
    /// only when the status really changes; returns the resulting status.
    /// </summary>
    public OrderStatus Apply(OrderTrigger trigger, string actorUserId, DateTime nowUtc, string? note = null)
    {
        var from = Status;
        var to = OrderStateMachine.Next(from, trigger);
        if (to == from)
        {
            return to;
        }

        Status = to;
        History.Add(new StatusChange(from, to, nowUtc, actorUserId, note));

        switch (to)
        {
            case OrderStatus.Paid:
                PaymentStatus = PaymentStatus.ConfirmedSimulated;
                PaidUtc = nowUtc;
                PaidByUserId = actorUserId;
                break;
            case OrderStatus.Cancelled:
                CancelledUtc = nowUtc;
                break;
            case OrderStatus.Active:
                ActivatedUtc = nowUtc;
                break;
        }

        return to;
    }
}
