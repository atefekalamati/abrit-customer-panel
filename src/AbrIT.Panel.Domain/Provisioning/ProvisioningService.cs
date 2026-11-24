using AbrIT.Panel.Domain.Orders;

namespace AbrIT.Panel.Domain.Provisioning;

public interface IProvisioningRequestRepository
{
    Task<ProvisioningRequest?> GetByOrderIdAsync(string orderId, CancellationToken ct = default);

    Task SaveAsync(ProvisioningRequest request, CancellationToken ct = default);
}

/// <summary>Customer e-mail for the Nextcloud welcome / set-password mail (module: Orchard <c>UserManager</c>).</summary>
public interface ICustomerDirectory
{
    Task<string?> GetEmailAsync(string userId, CancellationToken ct = default);
}

public sealed record ProvisioningOutcome(Order Order, ProvisioningRequest? Request);

public interface IProvisioningService
{
    /// <summary>
    /// Provisions a <c>Paid</c> order under the per-order lock. Idempotent: <c>Active</c>, in-progress and
    /// not-yet-paid orders are returned unchanged; at most one <see cref="ProvisioningRequest"/> per order.
    /// </summary>
    /// <exception cref="OrderNotFoundException">No such order.</exception>
    Task<ProvisioningOutcome> ProvisionAsync(string orderId, CancellationToken ct = default);

    /// <summary>Admin-only controlled retry from <c>Failed</c> while attempts remain (DECISIONS A-02).</summary>
    /// <exception cref="OperatorNotAuthorizedException">Actor lacks <see cref="OperatorAction.RetryProvisioning"/>.</exception>
    /// <exception cref="OrderNotFoundException">No such order.</exception>
    /// <exception cref="InvalidOrderTransitionException">The order is not <c>Failed</c>.</exception>
    /// <exception cref="ProvisioningAttemptsExhaustedException">MaxAttempts reached.</exception>
    Task<ProvisioningOutcome> RetryAsync(string orderId, string adminUserId, CancellationToken ct = default);
}

public sealed class ProvisioningAttemptsExhaustedException(int maxAttempts)
    : InvalidOperationException($"Provisioning has reached the maximum of {maxAttempts} attempts.")
{
    public int MaxAttempts { get; } = maxAttempts;
}

public sealed class ProvisioningService(
    IOrderRepository orders,
    IProvisioningRequestRepository requests,
    INextcloudProvisioner provisioner,
    ICustomerDirectory customers,
    IOperatorAuthorizer authorizer,
    IOrderLock orderLock,
    IIdSource ids,
    IClock clock) : IProvisioningService
{
    public async Task<ProvisioningOutcome> ProvisionAsync(string orderId, CancellationToken ct = default)
    {
        await using (await orderLock.AcquireAsync(OrderService.LockKey(orderId), ct))
        {
            var order = await orders.GetAsync(orderId, ct) ?? throw new OrderNotFoundException(orderId);
            var request = await requests.GetByOrderIdAsync(orderId, ct);

            // Layer 1: only a Paid order may enter provisioning here (Failed goes through RetryAsync), and only
            // once: an existing request that has been attempted is never re-run from this path.
            if (order.Status != OrderStatus.Paid || request is { Status: not ProvisioningStatus.Pending })
            {
                return new(order, request);
            }

            // Layer 2: one request per order, created inside the lock.
            request ??= CreateRequest(order);
            order.Apply(OrderTrigger.Start, SystemActors.Provisioning, clock.UtcNow);
            return await RunAttemptAsync(order, request, ct);
        }
    }

    public async Task<ProvisioningOutcome> RetryAsync(string orderId, string adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(adminUserId)
            || !await authorizer.IsAuthorizedAsync(adminUserId, OperatorAction.RetryProvisioning, ct))
        {
            throw new OperatorNotAuthorizedException(OperatorAction.RetryProvisioning);
        }

        await using (await orderLock.AcquireAsync(OrderService.LockKey(orderId), ct))
        {
            var order = await orders.GetAsync(orderId, ct) ?? throw new OrderNotFoundException(orderId);
            if (!OrderStateMachine.CanApply(order.Status, OrderTrigger.Retry))
            {
                throw new InvalidOrderTransitionException(order.Status, OrderTrigger.Retry);
            }

            var request = await requests.GetByOrderIdAsync(orderId, ct) ?? CreateRequest(order);
            if (request.Attempts >= request.MaxAttempts)
            {
                throw new ProvisioningAttemptsExhaustedException(request.MaxAttempts);
            }

            order.Apply(OrderTrigger.Retry, adminUserId, clock.UtcNow, $"Retry #{request.Attempts + 1}");
            return await RunAttemptAsync(order, request, ct);
        }
    }

    private ProvisioningRequest CreateRequest(Order order)
    {
        var externalUserId = ExternalIds.ExternalUserId(order);
        return new ProvisioningRequest
        {
            RequestId = ids.NewId(),
            OrderId = order.OrderId,
            IdempotencyKey = OrderService.LockKey(order.OrderId),
            Mode = provisioner.Mode,
            ExternalUserId = externalUserId,
            ExternalGroupId = order.PlanSnapshot.MaxUsers > 1 ? ExternalIds.TeamGroupId(externalUserId) : null,
            QuotaBytes = ExternalIds.QuotaBytes(order.PlanSnapshot),
            Status = ProvisioningStatus.Pending,
            CreatedUtc = clock.UtcNow,
        };
    }

    /// <summary>Order is already <c>Provisioning</c>. Persists the in-progress state before calling out.</summary>
    private async Task<ProvisioningOutcome> RunAttemptAsync(Order order, ProvisioningRequest request, CancellationToken ct)
    {
        request.Status = ProvisioningStatus.InProgress;
        request.Attempts++;
        request.LastAttemptUtc = clock.UtcNow;
        await requests.SaveAsync(request, ct);
        await orders.SaveAsync(order, ct);

        ProvisionResult result;
        try
        {
            result = await CallConnectorAsync(order, request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Only the exception type is kept: messages of transport exceptions may echo URLs or headers.
            result = ProvisionResult.Error("connector_exception", ex.GetType().Name);
        }

        var now = clock.UtcNow;
        if (result.Succeeded)
        {
            request.Status = ProvisioningStatus.Succeeded;
            request.SucceededUtc = now;
            request.LastErrorCode = null;
            request.LastErrorMessage = null;
            order.Apply(OrderTrigger.Succeed, SystemActors.Provisioning, now, $"Mode: {request.Mode}");
        }
        else
        {
            request.Status = ProvisioningStatus.Failed;
            request.LastErrorCode = ErrorSanitizer.Sanitize(result.ErrorCode ?? "error");
            request.LastErrorMessage = ErrorSanitizer.Sanitize(result.ErrorMessage);
            order.Apply(OrderTrigger.Fail, SystemActors.Provisioning, now, request.LastErrorCode);
        }

        await requests.SaveAsync(request, ct);
        await orders.SaveAsync(order, ct);
        return new(order, request);
    }

    private async Task<ProvisionResult> CallConnectorAsync(Order order, ProvisioningRequest request, CancellationToken ct)
    {
        if (request.ExternalGroupId is { } groupId)
        {
            var group = await provisioner.EnsureGroupAsync(groupId, ct);
            if (!group.Succeeded)
            {
                return group;
            }
        }

        // Layer 3: deterministic id + create-if-missing at the connector.
        var email = await customers.GetEmailAsync(order.OwnerUserId, ct);
        var user = await provisioner.EnsureUserAsync(
            new EnsureUserCommand(
                request.ExternalUserId,
                order.OwnerUserName,
                email,
                request.ExternalGroupId is null ? [] : [request.ExternalGroupId]),
            ct);
        if (!user.Succeeded)
        {
            return user;
        }

        return await provisioner.SetQuotaAsync(request.ExternalUserId, request.QuotaBytes, ct);
    }
}
