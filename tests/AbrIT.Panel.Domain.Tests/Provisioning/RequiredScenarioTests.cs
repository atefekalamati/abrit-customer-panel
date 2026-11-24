using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Domain.Tests.Orders;
using AbrIT.Panel.Nextcloud;

namespace AbrIT.Panel.Domain.Tests.Provisioning;

/// <summary>Wires the real Domain services with in-memory adapters and the real Mock connector.</summary>
internal sealed class Harness
{
    public const string Admin = "admin-1";
    public const string UserA = "user-a";
    public const string UserB = "user-b";
    public const string Starter = "abrplan-drive-starter";
    public const string Team = "abrplan-drive-team";

    public Harness(INextcloudProvisioner? provisioner = null)
    {
        Connector = provisioner ?? Mock;
        var ids = new CountingIdSource();
        var numbers = new SequentialOrderNumberGenerator(new RacySequenceStore(), Lock, Clock);
        var authorizer = new FakeOperatorAuthorizer(Admin);

        Orders = new OrderService(OrderRepo, new FakePlanCatalog(), numbers, Lock, Clock, ids);
        Provisioning = new ProvisioningService(
            OrderRepo, Requests, Connector, new FakeCustomerDirectory(), authorizer, Lock, ids, Clock);
        Approval = new PaymentApprovalService(OrderRepo, Lock, authorizer, Provisioning, Clock);
        AdminOrders = new AdminOrderService(OrderRepo, Lock, authorizer, Clock);
    }

    public FakeClock Clock { get; } = new(new DateTime(2026, 11, 23, 9, 30, 0, DateTimeKind.Utc));

    public SemaphoreOrderLock Lock { get; } = new();

    public InMemoryOrderRepository OrderRepo { get; } = new();

    public InMemoryProvisioningRequestRepository Requests { get; } = new();

    public MockNextcloudProvisioner Mock { get; } = new();

    public INextcloudProvisioner Connector { get; }

    public OrderService Orders { get; }

    public ProvisioningService Provisioning { get; }

    public PaymentApprovalService Approval { get; }

    public AdminOrderService AdminOrders { get; }

    public Task<Order> PlaceAsync(string user = UserA, string plan = Starter) => Orders.PlaceOrderAsync(user, user, plan);
}

public class RequiredScenarioTests
{
    private const long OneGigabyte = 1_073_741_824;

    // Required test 1 (AC-07)
    [Fact]
    public async Task Ownership_CustomerB_CannotReadCustomerA_Order()
    {
        var h = new Harness();
        var orderOfA = await h.PlaceAsync(Harness.UserA);

        Assert.Null(await h.Orders.GetForOwnerAsync(Harness.UserB, orderOfA.OrderId));
        Assert.Empty(await h.Orders.ListForOwnerAsync(Harness.UserB));
        Assert.Null(await h.Orders.CancelAsync(Harness.UserB, orderOfA.OrderId));

        var own = await h.Orders.GetForOwnerAsync(Harness.UserA, orderOfA.OrderId);
        Assert.NotNull(own);
        Assert.Equal(OrderStatus.PendingPayment, own.Status);
    }

    // Required test 2 (AC-05)
    [Fact]
    public async Task Provisioning_ApproveTwiceAndCallback_CreatesExternalAccountOnce()
    {
        var h = new Harness();

        // Sequential: approve, approve again, then the callback for the same order.
        var first = await h.PlaceAsync();
        var a1 = await h.Approval.ApproveAsync(first.OrderId, Harness.Admin);
        var a2 = await h.Approval.ApproveAsync(first.OrderId, Harness.Admin);
        var cb = await h.Approval.ApproveFromVerifiedCallbackAsync(first.OrderId);

        Assert.Equal(OrderStatus.Active, a1.Status);
        Assert.Equal(OrderStatus.Active, a2.Status);
        Assert.Equal(OrderStatus.Active, cb.Status);
        Assert.Equal(1, h.Mock.CreateUserCalls);
        Assert.Equal(1, h.Mock.SetQuotaCalls);

        // Parallel: two admin approvals racing the payment callback on a fresh order.
        var second = await h.PlaceAsync(Harness.UserB);
        var results = await Task.WhenAll(
            Task.Run(() => h.Approval.ApproveAsync(second.OrderId, Harness.Admin)),
            Task.Run(() => h.Approval.ApproveFromVerifiedCallbackAsync(second.OrderId)),
            Task.Run(() => h.Approval.ApproveAsync(second.OrderId, Harness.Admin)));

        Assert.All(results, o => Assert.Equal(OrderStatus.Active, o.Status));
        Assert.Equal(2, h.Mock.CreateUserCalls); // one per order
        Assert.Equal(2, h.Mock.SetQuotaCalls);
        Assert.Equal(2, h.Requests.Count);

        var stored = await h.OrderRepo.GetAsync(second.OrderId);
        Assert.Single(stored!.History, c => c.To == OrderStatus.Paid);
        Assert.Single(stored.History, c => c.To == OrderStatus.Active);
        var request = await h.Requests.GetByOrderIdAsync(second.OrderId);
        Assert.Equal(1, request!.Attempts);
    }

    // Required test 3 (AC-06)
    [Fact]
    public async Task Provisioning_Failure_SetsFailed_ThenRetry_Activates_AndRespectsMaxAttempts()
    {
        var h = new Harness();

        // One injected failure → Failed, then a controlled retry → Active.
        h.Mock.FailNextCalls = 1;
        var order = await h.PlaceAsync();
        var failed = await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);

        Assert.Equal(OrderStatus.Failed, failed.Status);
        Assert.Equal(PaymentStatus.ConfirmedSimulated, failed.PaymentStatus);
        var request = await h.Requests.GetByOrderIdAsync(order.OrderId);
        Assert.Equal(ProvisioningStatus.Failed, request!.Status);
        Assert.Equal(1, request.Attempts);
        Assert.Equal(MockNextcloudProvisioner.InjectedFailureCode, request.LastErrorCode);
        Assert.Equal(0, h.Mock.CreateUserCalls);

        // Approving a failed (already paid) order again is a no-op, not a hidden retry.
        Assert.Equal(OrderStatus.Failed, (await h.Approval.ApproveAsync(order.OrderId, Harness.Admin)).Status);
        Assert.Equal(1, request.Attempts);

        var retried = await h.Provisioning.RetryAsync(order.OrderId, Harness.Admin);
        Assert.Equal(OrderStatus.Active, retried.Order.Status);
        Assert.Equal(ProvisioningStatus.Succeeded, retried.Request!.Status);
        Assert.Equal(2, retried.Request.Attempts);
        Assert.Null(retried.Request.LastErrorMessage);
        Assert.Equal(1, h.Mock.CreateUserCalls);
        await Assert.ThrowsAsync<InvalidOrderTransitionException>(() => h.Provisioning.RetryAsync(order.OrderId, Harness.Admin));

        // Persistent failure: attempts are capped at MaxAttempts (5).
        h.Mock.FailNextCalls = 100;
        var doomed = await h.PlaceAsync(Harness.UserB);
        await h.Approval.ApproveAsync(doomed.OrderId, Harness.Admin); // attempt 1
        for (var i = 2; i <= ProvisioningRequest.DefaultMaxAttempts; i++)
        {
            var outcome = await h.Provisioning.RetryAsync(doomed.OrderId, Harness.Admin);
            Assert.Equal(OrderStatus.Failed, outcome.Order.Status);
            Assert.Equal(i, outcome.Request!.Attempts);
        }

        await Assert.ThrowsAsync<ProvisioningAttemptsExhaustedException>(
            () => h.Provisioning.RetryAsync(doomed.OrderId, Harness.Admin));
        var capped = await h.Requests.GetByOrderIdAsync(doomed.OrderId);
        Assert.Equal(ProvisioningRequest.DefaultMaxAttempts, capped!.Attempts);
        Assert.False(capped.CanRetry);
        Assert.Equal(OrderStatus.Failed, (await h.OrderRepo.GetAsync(doomed.OrderId))!.Status);
        Assert.Equal(1, h.Mock.CreateUserCalls);
    }

    [Fact]
    public async Task Approve_on_cancelled_order_is_rejected_and_nothing_is_provisioned()
    {
        var h = new Harness();
        var order = await h.PlaceAsync();
        await h.Orders.CancelAsync(Harness.UserA, order.OrderId);

        await Assert.ThrowsAsync<InvalidOrderTransitionException>(() => h.Approval.ApproveAsync(order.OrderId, Harness.Admin));
        await Assert.ThrowsAsync<InvalidOrderTransitionException>(() => h.Approval.ApproveFromVerifiedCallbackAsync(order.OrderId));

        Assert.Equal(OrderStatus.Cancelled, (await h.OrderRepo.GetAsync(order.OrderId))!.Status);
        Assert.Equal(0, h.Requests.Count);
        Assert.Equal(0, h.Mock.CreateUserCalls);
    }

    [Fact]
    public async Task Approve_unknown_order_throws_not_found()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<OrderNotFoundException>(() => h.Approval.ApproveAsync("missing", Harness.Admin));
    }

    [Theory]
    [InlineData(Harness.UserA)] // the owner is not an operator either
    [InlineData(Harness.UserB)]
    [InlineData("")]
    public async Task NonAdmin_cannot_approve_retry_or_admin_cancel(string actor)
    {
        var h = new Harness();
        var pending = await h.PlaceAsync();

        await Assert.ThrowsAsync<OperatorNotAuthorizedException>(() => h.Approval.ApproveAsync(pending.OrderId, actor));
        await Assert.ThrowsAsync<OperatorNotAuthorizedException>(() => h.AdminOrders.CancelAsync(pending.OrderId, actor));
        Assert.Equal(OrderStatus.PendingPayment, (await h.OrderRepo.GetAsync(pending.OrderId))!.Status);

        h.Mock.FailNextCalls = 1;
        var failed = await h.PlaceAsync(Harness.UserB);
        await h.Approval.ApproveAsync(failed.OrderId, Harness.Admin);
        await Assert.ThrowsAsync<OperatorNotAuthorizedException>(() => h.Provisioning.RetryAsync(failed.OrderId, actor));
        Assert.Equal(OrderStatus.Failed, (await h.OrderRepo.GetAsync(failed.OrderId))!.Status);
        Assert.Equal(1, (await h.Requests.GetByOrderIdAsync(failed.OrderId))!.Attempts);
    }

    [Fact]
    public async Task Customer_cannot_cancel_another_customers_order_but_admin_can()
    {
        var h = new Harness();
        var order = await h.PlaceAsync(Harness.UserA);

        Assert.Null(await h.Orders.CancelAsync(Harness.UserB, order.OrderId));
        Assert.Equal(OrderStatus.PendingPayment, (await h.OrderRepo.GetAsync(order.OrderId))!.Status);

        var cancelled = await h.AdminOrders.CancelAsync(order.OrderId, Harness.Admin);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(Harness.Admin, cancelled.History[^1].ActorUserId);
    }

    [Fact]
    public async Task Starter_quota_is_1073741824_bytes_on_request_and_connector()
    {
        var h = new Harness();
        var order = await h.PlaceAsync();
        await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);

        var request = await h.Requests.GetByOrderIdAsync(order.OrderId);
        Assert.Equal(OneGigabyte, request!.QuotaBytes);
        var account = await h.Mock.GetUserAsync(request.ExternalUserId, default);
        Assert.Equal(OneGigabyte, account!.QuotaBytes);
        Assert.Null(request.ExternalGroupId);
    }

    [Fact]
    public async Task External_user_id_is_derived_from_order_number_and_mode_is_recorded()
    {
        var h = new Harness();
        var order = await h.PlaceAsync();
        await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);

        var request = await h.Requests.GetByOrderIdAsync(order.OrderId);
        Assert.Equal("ABR-1405-000001", order.OrderNumber);
        Assert.Equal("abrit-ord1405-000001", request!.ExternalUserId);
        Assert.Equal($"order:{order.OrderId}", request.IdempotencyKey);
        Assert.Equal(ProvisionerMode.Mock, request.Mode);
    }

    [Fact]
    public async Task Team_plan_creates_group_and_500MB_per_user_quota()
    {
        var h = new Harness();
        var order = await h.PlaceAsync(plan: Harness.Team);
        var result = await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);

        var request = await h.Requests.GetByOrderIdAsync(order.OrderId);
        Assert.Equal(OrderStatus.Active, result.Status);
        Assert.Equal("abrit-ord1405-000001-team", request!.ExternalGroupId);
        Assert.Equal(500L * 1024 * 1024, request.QuotaBytes);
        Assert.Equal(1, h.Mock.CreateGroupCalls);
    }

    [Fact]
    public async Task Connector_exception_becomes_Failed_with_safe_error()
    {
        var h = new Harness(new NotConfiguredRealProvisioner());
        var order = await h.PlaceAsync();
        var result = await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);

        var request = await h.Requests.GetByOrderIdAsync(order.OrderId);
        Assert.Equal(OrderStatus.Failed, result.Status);
        Assert.Equal(ProvisionerMode.Real, request!.Mode);
        Assert.Equal("connector_exception", request.LastErrorCode);
        Assert.Equal(nameof(NotImplementedException), request.LastErrorMessage);
    }
}
