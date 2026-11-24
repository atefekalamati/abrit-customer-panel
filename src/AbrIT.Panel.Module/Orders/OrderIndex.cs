using AbrIT.Panel.Domain.Orders;
using YesSql.Indexes;

namespace AbrIT.Panel.Module.Orders;

public sealed class OrderIndex : MapIndex
{
    public string OrderId { get; set; } = string.Empty;

    public string OrderNumber { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}

public sealed class OrderIndexProvider : IndexProvider<Order>
{
    public override void Describe(DescribeContext<Order> context) =>
        context.For<OrderIndex>().Map(order => new OrderIndex
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber,
            OwnerUserId = order.OwnerUserId,
            Status = order.Status.ToString(),
            CreatedUtc = order.CreatedUtc,
        });
}
