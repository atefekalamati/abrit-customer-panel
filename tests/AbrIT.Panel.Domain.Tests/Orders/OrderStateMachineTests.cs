using AbrIT.Panel.Domain.Orders;

namespace AbrIT.Panel.Domain.Tests.Orders;

public sealed class OrderStateMachineTests
{
    // The complete §4.3 graph. Every (status, trigger) pair not listed here must throw.
    private static readonly Dictionary<(OrderStatus From, OrderTrigger Trigger), OrderStatus> Valid = new()
    {
        [(OrderStatus.PendingPayment, OrderTrigger.Approve)] = OrderStatus.Paid,
        [(OrderStatus.PendingPayment, OrderTrigger.Cancel)] = OrderStatus.Cancelled,
        [(OrderStatus.Paid, OrderTrigger.Start)] = OrderStatus.Provisioning,
        [(OrderStatus.Provisioning, OrderTrigger.Succeed)] = OrderStatus.Active,
        [(OrderStatus.Provisioning, OrderTrigger.Fail)] = OrderStatus.Failed,
        [(OrderStatus.Failed, OrderTrigger.Retry)] = OrderStatus.Provisioning,
        // Idempotent approve: no-op returning the current state.
        [(OrderStatus.Paid, OrderTrigger.Approve)] = OrderStatus.Paid,
        [(OrderStatus.Provisioning, OrderTrigger.Approve)] = OrderStatus.Provisioning,
        [(OrderStatus.Active, OrderTrigger.Approve)] = OrderStatus.Active,
    };

    public static TheoryData<OrderStatus, OrderTrigger, OrderStatus> ValidTransitions()
    {
        var data = new TheoryData<OrderStatus, OrderTrigger, OrderStatus>();
        foreach (var ((from, trigger), to) in Valid)
        {
            data.Add(from, trigger, to);
        }

        return data;
    }

    public static TheoryData<OrderStatus, OrderTrigger> InvalidTransitions()
    {
        var data = new TheoryData<OrderStatus, OrderTrigger>();
        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var trigger in Enum.GetValues<OrderTrigger>())
            {
                if (!Valid.ContainsKey((from, trigger)))
                {
                    data.Add(from, trigger);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void Valid_transition_returns_expected_state(OrderStatus from, OrderTrigger trigger, OrderStatus expected)
    {
        Assert.Equal(expected, OrderStateMachine.Next(from, trigger));
        Assert.True(OrderStateMachine.CanApply(from, trigger));
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void Invalid_transition_throws(OrderStatus from, OrderTrigger trigger)
    {
        var ex = Assert.Throws<InvalidOrderTransitionException>(() => OrderStateMachine.Next(from, trigger));
        Assert.Equal(from, ex.From);
        Assert.Equal(trigger, ex.Trigger);
        Assert.False(OrderStateMachine.CanApply(from, trigger));
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment)]
    [InlineData(OrderStatus.Cancelled)]
    public void Unpaid_or_cancelled_orders_can_never_start_provisioning(OrderStatus from)
    {
        Assert.Throws<InvalidOrderTransitionException>(() => OrderStateMachine.Next(from, OrderTrigger.Start));
        Assert.Throws<InvalidOrderTransitionException>(() => OrderStateMachine.Next(from, OrderTrigger.Retry));
    }

    [Fact]
    public void Matrix_covers_every_pair()
    {
        var total = Enum.GetValues<OrderStatus>().Length * Enum.GetValues<OrderTrigger>().Length;
        Assert.Equal(total, ValidTransitions().Count + InvalidTransitions().Count);
    }

    [Fact]
    public void Order_apply_records_history_only_for_real_changes()
    {
        var now = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var order = new Order { OrderId = "o1", Status = OrderStatus.PendingPayment };

        Assert.Equal(OrderStatus.Paid, order.Apply(OrderTrigger.Approve, "admin", now));
        Assert.Equal(OrderStatus.Paid, order.Apply(OrderTrigger.Approve, "admin", now.AddMinutes(1)));

        var change = Assert.Single(order.History);
        Assert.Equal(OrderStatus.PendingPayment, change.From);
        Assert.Equal(OrderStatus.Paid, change.To);
        Assert.Equal("admin", change.ActorUserId);
        Assert.Equal(now, change.AtUtc);
    }
}
