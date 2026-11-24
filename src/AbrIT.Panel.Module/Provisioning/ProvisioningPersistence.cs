using AbrIT.Panel.Domain.Provisioning;
using YesSql;
using YesSql.Indexes;

namespace AbrIT.Panel.Module.Provisioning;

public sealed class ProvisioningRequestIndex : MapIndex
{
    public string OrderId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public sealed class ProvisioningRequestIndexProvider : IndexProvider<ProvisioningRequest>
{
    public override void Describe(DescribeContext<ProvisioningRequest> context) =>
        context.For<ProvisioningRequestIndex>().Map(request => new ProvisioningRequestIndex
        {
            OrderId = request.OrderId,
            Status = request.Status.ToString(),
        });
}

/// <summary>
/// Commits on every save (like <c>YesSqlOrderRepository</c>), so state written inside the per-order lock is
/// durable and visible to the next lock holder before the lock is released (DECISIONS D-12).
/// </summary>
public sealed class YesSqlProvisioningRequestRepository(ISession session) : IProvisioningRequestRepository
{
    public Task<ProvisioningRequest?> GetByOrderIdAsync(string orderId, CancellationToken ct = default) =>
        session.Query<ProvisioningRequest, ProvisioningRequestIndex>(x => x.OrderId == orderId).FirstOrDefaultAsync(ct)!;

    public async Task SaveAsync(ProvisioningRequest request, CancellationToken ct = default)
    {
        await session.SaveAsync(request, cancellationToken: ct);
        await session.SaveChangesAsync(ct);
    }
}
