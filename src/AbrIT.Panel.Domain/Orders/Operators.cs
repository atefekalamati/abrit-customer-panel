namespace AbrIT.Panel.Domain.Orders;

/// <summary>Back-office actions that need an operator permission (module maps them to Orchard permissions).</summary>
public enum OperatorAction
{
    ApprovePayment,
    RetryProvisioning,
    CancelOrder,
}

/// <summary>Decides whether a back-office actor may perform an action (module: Orchard <c>IAuthorizationService</c>).</summary>
public interface IOperatorAuthorizer
{
    Task<bool> IsAuthorizedAsync(string actorUserId, OperatorAction action, CancellationToken ct = default);
}

/// <summary>Non-user actors recorded in order history.</summary>
public static class SystemActors
{
    /// <summary>The HMAC-verified mock payment callback (DECISIONS A-09).</summary>
    public const string PaymentCallback = "payment-callback";

    /// <summary>Provisioning steps that run as a consequence of an approved payment.</summary>
    public const string Provisioning = "provisioning";
}

public sealed class OperatorNotAuthorizedException(OperatorAction action)
    : UnauthorizedAccessException($"The current actor is not allowed to perform '{action}'.")
{
    public OperatorAction Action { get; } = action;
}

public sealed class OrderNotFoundException(string orderId)
    : InvalidOperationException("The order does not exist.")
{
    public string OrderId { get; } = orderId;
}
