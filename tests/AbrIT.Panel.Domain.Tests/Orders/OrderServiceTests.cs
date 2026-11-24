using AbrIT.Panel.Domain.Orders;

namespace AbrIT.Panel.Domain.Tests.Orders;

public sealed class OrderServiceTests
{
    private const string UserA = "user-a";
    private const string UserB = "user-b";
    private const string Starter = "abrplan-drive-starter";

    private readonly FakeClock _clock = new(new DateTime(2026, 11, 23, 9, 30, 0, DateTimeKind.Utc));
    private readonly InMemoryOrderRepository _repository = new();
    private readonly FakePlanCatalog _catalog = new();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        var orderLock = new SemaphoreOrderLock();
        var numbers = new SequentialOrderNumberGenerator(new RacySequenceStore(), orderLock, _clock);
        _service = new OrderService(_repository, _catalog, numbers, orderLock, _clock, new CountingIdSource());
    }

    [Fact]
    public async Task AC02_customer_orders_starter_and_gets_PendingPayment_with_frozen_snapshot()
    {
        var order = await _service.PlaceOrderAsync(UserA, "customer-a", Starter);

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal(UserA, order.OwnerUserId);
        Assert.Equal("customer-a", order.OwnerUserName);
        Assert.Equal(Starter, order.PlanContentItemId);
        Assert.Equal(new PlanSnapshot("drive-starter", "درایو – پایه", 99_000, 1024, 1), order.PlanSnapshot);
        Assert.Equal(99_000, order.AmountToman);
        Assert.Equal(_clock.UtcNow, order.CreatedUtc);
        Assert.Matches(@"^ABR-1405-\d{6}$", order.OrderNumber);
        Assert.False(string.IsNullOrEmpty(order.OrderId));
        Assert.Null(order.PaidUtc);
        Assert.Empty(order.History);

        var stored = await _service.GetForOwnerAsync(UserA, order.OrderId);
        Assert.NotNull(stored);
        Assert.Equal(OrderStatus.PendingPayment, stored.Status);
    }

    [Fact]
    public async Task Snapshot_is_not_affected_by_later_price_changes()
    {
        var order = await _service.PlaceOrderAsync(UserA, "customer-a", Starter);

        _catalog.Plans[Starter] = new(new PlanSnapshot("drive-starter", "درایو – پایه", 150_000, 2048, 1), true);

        var stored = await _service.GetForOwnerAsync(UserA, order.OrderId);
        Assert.Equal(99_000, stored!.AmountToman);
        Assert.Equal(1024, stored.PlanSnapshot.QuotaPerUserMB);
    }

    [Theory]
    [InlineData("abrplan-retired")] // exists but IsOrderable = false
    [InlineData("abrplan-unknown")] // does not exist
    [InlineData("")]
    [InlineData("../etc/passwd")] // malformed id
    public async Task Not_orderable_unknown_or_malformed_plan_is_rejected(string planId)
    {
        await Assert.ThrowsAsync<PlanNotOrderableException>(() => _service.PlaceOrderAsync(UserA, "customer-a", planId));
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task GetForOwner_returns_null_for_another_users_order()
    {
        var order = await _service.PlaceOrderAsync(UserA, "customer-a", Starter);

        Assert.Null(await _service.GetForOwnerAsync(UserB, order.OrderId));
        Assert.Null(await _service.GetForOwnerAsync(UserA, "does-not-exist"));
    }

    [Fact]
    public async Task ListForOwner_returns_only_own_orders_newest_first()
    {
        var first = await _service.PlaceOrderAsync(UserA, "customer-a", Starter);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        var second = await _service.PlaceOrderAsync(UserA, "customer-a", "abrplan-drive-team");
        await _service.PlaceOrderAsync(UserB, "customer-b", Starter);

        var list = await _service.ListForOwnerAsync(UserA);

        Assert.Equal([second.OrderId, first.OrderId], list.Select(o => o.OrderId));
    }

    [Fact]
    public async Task Owner_can_cancel_pending_order()
    {
        var order = await _service.PlaceOrderAsync(UserA, "customer-a", Starter);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);

        var cancelled = await _service.CancelAsync(UserA, order.OrderId);

        Assert.NotNull(cancelled);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(_clock.UtcNow, cancelled.CancelledUtc);
        var change = Assert.Single(cancelled.History);
        Assert.Equal(OrderStatus.PendingPayment, change.From);
        Assert.Equal(OrderStatus.Cancelled, change.To);
        Assert.Equal(UserA, change.ActorUserId);
    }

    [Fact]
    public async Task Other_user_cannot_cancel_and_order_is_unchanged()
    {
        var order = await _service.PlaceOrderAsync(UserA, "customer-a", Starter);
        var savesBefore = _repository.SaveCount;

        Assert.Null(await _service.CancelAsync(UserB, order.OrderId));

        Assert.Equal(savesBefore, _repository.SaveCount);
        Assert.Equal(OrderStatus.PendingPayment, (await _service.GetForOwnerAsync(UserA, order.OrderId))!.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Provisioning)]
    [InlineData(OrderStatus.Active)]
    [InlineData(OrderStatus.Failed)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Cancel_is_rejected_unless_PendingPayment(OrderStatus status)
    {
        var order = await _service.PlaceOrderAsync(UserA, "customer-a", Starter);
        (await _repository.GetAsync(order.OrderId))!.Status = status;
        var savesBefore = _repository.SaveCount;

        await Assert.ThrowsAsync<InvalidOrderTransitionException>(() => _service.CancelAsync(UserA, order.OrderId));

        Assert.Equal(savesBefore, _repository.SaveCount);
        Assert.Equal(status, (await _repository.GetAsync(order.OrderId))!.Status);
    }
}
