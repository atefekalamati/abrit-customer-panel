using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Domain.Workspaces;
using AbrIT.Panel.Module.Catalog;

namespace AbrIT.Panel.Module.ViewModels;

public sealed record DriveViewModel(ServiceCard Service, IReadOnlyList<PlanCard> Plans);

public sealed record CustomerOrderViewModel(Order Order, ProvisioningRequest? Request, WorkspaceView? Workspace);

public sealed record AdminOrderRow(Order Order, ProvisioningRequest? Request, TeamWorkspace? Workspace);

public sealed record AdminOrdersViewModel(
    IReadOnlyList<AdminOrderRow> Rows, OrderStatus? Filter, int Page, int PageCount, int Total, bool CanManage);

public sealed record ProvisioningSettingsViewModel(
    ProvisionerMode Mode, string? BaseUrl, bool AppPasswordSet, int MaxAttempts, int? MockFailNextCalls, int? MockAccounts, int? MockGroups);

/// <summary>Order review (/orders/new): plan + owner shown back to the customer before POST.</summary>
public sealed record OrderReviewViewModel(PlanCard Plan, string? ServiceTitle, string OwnerName, string? OwnerEmail);

public sealed record DashboardOrderRow(Order Order, ProvisionerMode? Mode);

/// <summary>Customer dashboard: only real numbers derived from the owner's orders (DESIGN_PROMPTS page 5).</summary>
public sealed record DashboardViewModel(string UserName, IReadOnlyList<DashboardOrderRow> Orders)
{
    public IReadOnlyList<DashboardOrderRow> Active => [.. Orders.Where(r => r.Order.Status == OrderStatus.Active)];

    /// <summary>Total quota of active services in MB (per-user quota × seats).</summary>
    public long AllocatedMB => Active.Sum(r => (long)r.Order.PlanSnapshot.QuotaPerUserMB * Math.Max(1, r.Order.PlanSnapshot.MaxUsers));

    public IReadOnlyList<(OrderStatus Status, int Count)> StatusBreakdown =>
        [.. Orders.GroupBy(r => r.Order.Status).OrderBy(g => g.Key == OrderStatus.Active ? 0 : 1).ThenBy(g => g.Key)
            .Select(g => (g.Key, g.Count()))];
}
