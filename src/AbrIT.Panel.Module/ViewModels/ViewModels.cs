using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Domain.Workspaces;
using AbrIT.Panel.Module.Catalog;

namespace AbrIT.Panel.Module.ViewModels;

public sealed record DriveViewModel(ServiceCard Service, IReadOnlyList<PlanCard> Plans);

/// <param name="DriveUrl">Real mode only: the Nextcloud address (Mock has no real address).</param>
public sealed record CustomerOrderViewModel(Order Order, ProvisioningRequest? Request, WorkspaceView? Workspace, string? DriveUrl = null)
{
    public ProvisionerMode? Mode => Request?.Mode;

    public bool IsMock => Request?.Mode == ProvisionerMode.Mock;

    public bool IsTeam => Order.PlanSnapshot.MaxUsers > 1;

    public int Attempts => Request?.Attempts ?? 0;

    public int MaxAttempts => Request?.MaxAttempts ?? ProvisioningRequest.DefaultMaxAttempts;
}

/// <summary>/dashboard/orders: every order of the signed-in customer, newest first.</summary>
public sealed record MyOrdersViewModel(IReadOnlyList<DashboardOrderRow> Orders);

public sealed record AdminOrderRow(Order Order, ProvisioningRequest? Request, TeamWorkspace? Workspace, string? OwnerEmail);

/// <param name="Counts">Orders per status card (Paid is counted with Provisioning).</param>
public sealed record AdminOrdersViewModel(
    IReadOnlyList<AdminOrderRow> Rows,
    OrderStatus? Filter,
    string? Search,
    int Page,
    int PageCount,
    int Total,
    bool CanManage,
    IReadOnlyDictionary<OrderStatus, int> Counts,
    int PageSize);

public sealed record ProvisioningSettingsViewModel(
    ProvisionerMode Mode,
    string? BaseUrl,
    string? AdminUser,
    bool AppPasswordSet,
    int MaxAttempts,
    DateTime? LastAttemptUtc,
    int? MockFailNextCalls,
    int? MockAccounts,
    int? MockGroups);

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
