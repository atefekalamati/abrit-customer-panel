using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Module.Catalog;

namespace AbrIT.Panel.Module.ViewModels;

public sealed record DriveViewModel(ServiceCard Service, IReadOnlyList<PlanCard> Plans);

public sealed record CustomerOrderViewModel(Order Order, ProvisioningRequest? Request);

public sealed record AdminOrderRow(Order Order, ProvisioningRequest? Request);

public sealed record AdminOrdersViewModel(
    IReadOnlyList<AdminOrderRow> Rows, OrderStatus? Filter, int Page, int PageCount, int Total, bool CanManage);

public sealed record ProvisioningSettingsViewModel(
    ProvisionerMode Mode, string? BaseUrl, bool AppPasswordSet, int MaxAttempts, int? MockFailNextCalls, int? MockAccounts);
