namespace AbrIT.Panel.Domain.Orders;

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Provisioning,
    Active,
    Failed,
    Cancelled,
}

public enum PaymentStatus
{
    Pending,
    ConfirmedSimulated,
}

/// <summary>Events that move an order through <see cref="OrderStateMachine"/>.</summary>
public enum OrderTrigger
{
    /// <summary>Admin (or signed mock callback) confirms the simulated payment.</summary>
    Approve,

    /// <summary>Provisioning begins for a paid order.</summary>
    Start,

    /// <summary>Connector reported success.</summary>
    Succeed,

    /// <summary>Connector reported an error.</summary>
    Fail,

    /// <summary>Admin retries a failed provisioning.</summary>
    Retry,

    /// <summary>Owner or admin cancels an unpaid order.</summary>
    Cancel,
}
