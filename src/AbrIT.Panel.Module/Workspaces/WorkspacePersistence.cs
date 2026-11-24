using AbrIT.Panel.Domain.Workspaces;
using AbrIT.Panel.Module.Orders;
using YesSql;
using YesSql.Indexes;

namespace AbrIT.Panel.Module.Workspaces;

public sealed class TeamWorkspaceIndex : MapIndex
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class TeamWorkspaceIndexProvider : IndexProvider<TeamWorkspace>
{
    public override void Describe(DescribeContext<TeamWorkspace> context) =>
        context.For<TeamWorkspaceIndex>().Map(workspace => new TeamWorkspaceIndex { OrderId = workspace.OrderId });
}

/// <summary>Commits on every save and reads fresh, like the order/provisioning repositories (DECISIONS D-12).</summary>
public sealed class YesSqlTeamWorkspaceRepository(ISession session) : ITeamWorkspaceRepository
{
    public Task<TeamWorkspace?> GetByOrderIdAsync(string orderId, CancellationToken ct = default) =>
        session.GetFreshAsync(() =>
            session.Query<TeamWorkspace, TeamWorkspaceIndex>(x => x.OrderId == orderId).FirstOrDefaultAsync(ct));

    public async Task SaveAsync(TeamWorkspace workspace, CancellationToken ct = default)
    {
        await session.SaveAsync(workspace, cancellationToken: ct);
        await session.SaveChangesAsync(ct);
    }
}
