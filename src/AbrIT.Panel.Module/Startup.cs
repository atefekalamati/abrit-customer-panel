using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Module.Catalog;
using AbrIT.Panel.Module.Orders;
using AbrIT.Panel.Module.Provisioning;
using AbrIT.Panel.Module.Recipes;
using AbrIT.Panel.Module.Security;
using AbrIT.Panel.Nextcloud;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace AbrIT.Panel.Module;

/// <remarks>
/// <see cref="NextcloudOptions"/> and <see cref="MockPaymentOptions"/> are bound and validated by the host
/// (<c>Program.cs</c>) so that a bad Real-mode configuration stops the app at startup; tenants inherit them.
/// </remarks>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<DemoUsersStep>();

        // Orders (phase 03): YesSql persistence + Orchard adapters for the pure Domain services.
        services.AddIndexProvider<OrderIndexProvider>();
        services.AddDataMigration<OrderMigrations>();
        services.AddScoped<IOrderRepository, YesSqlOrderRepository>();
        services.AddScoped<ISequenceStore, YesSqlSequenceStore>();
        services.AddScoped<IPlanCatalog, ContentPlanCatalog>();
        services.AddSingleton<AbrIT.Panel.Domain.Orders.IClock, OrchardClock>();
        services.AddSingleton<IIdSource, OrchardIdSource>();
        services.AddSingleton<IOrderLock, DistributedOrderLock>();
        services.AddScoped<IOrderNumberGenerator, SequentialOrderNumberGenerator>();
        services.AddScoped<IOrderService, OrderService>();

        // Admin + payment (phase 04).
        services.AddPermissionProvider<PermissionProvider>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddHttpContextAccessor();
        services.AddScoped<IOperatorAuthorizer, OrchardOperatorAuthorizer>();
        services.AddScoped<IPaymentApprovalService, PaymentApprovalService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
        services.AddScoped<CatalogReader>();

        // Provisioning (phase 05). One connector per tenant, chosen by Nextcloud:Mode.
        services.AddIndexProvider<ProvisioningRequestIndexProvider>();
        services.AddScoped<IProvisioningRequestRepository, YesSqlProvisioningRequestRepository>();
        services.AddScoped<ICustomerDirectory, UserManagerCustomerDirectory>();
        services.AddScoped<IProvisioningService, ProvisioningService>();
        services.AddSingleton(sp => new MockNextcloudProvisioner(sp.GetRequiredService<NextcloudOptions>().Mock.FailNextCalls));
        services.AddSingleton<INextcloudProvisioner>(sp =>
            sp.GetRequiredService<NextcloudOptions>().Mode == ProvisionerMode.Real
                ? new NotConfiguredRealProvisioner()
                : sp.GetRequiredService<MockNextcloudProvisioner>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var admin = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;

        Map(routes, "AbrIT.Services", "services", "Services", "Index");
        Map(routes, "AbrIT.Services.Drive", "services/drive", "Services", "Drive");
        Map(routes, "AbrIT.Orders.New", "orders/new", "Orders", "New");
        Map(routes, "AbrIT.Dashboard", "dashboard", "Dashboard", "Index");
        Map(routes, "AbrIT.Dashboard.Order", "dashboard/orders/{orderId}", "Dashboard", "Order");
        Map(routes, "AbrIT.Dashboard.Cancel", "dashboard/orders/{orderId}/cancel", "Dashboard", "Cancel");
        Map(routes, "AbrIT.MockPaymentCallback", "api/payments/mock-callback", "MockPaymentCallback", "Post");

        Map(routes, "AbrIT.Admin.Orders", $"{admin}/AbrIT/Orders", "OrdersAdmin", "Index");
        Map(routes, "AbrIT.Admin.Orders.Approve", $"{admin}/AbrIT/Orders/{{orderId}}/ApprovePayment", "OrdersAdmin", "ApprovePayment");
        Map(routes, "AbrIT.Admin.Orders.Retry", $"{admin}/AbrIT/Orders/{{orderId}}/Retry", "OrdersAdmin", "Retry");
        Map(routes, "AbrIT.Admin.Orders.Cancel", $"{admin}/AbrIT/Orders/{{orderId}}/Cancel", "OrdersAdmin", "Cancel");
        Map(routes, "AbrIT.Admin.Provisioning", $"{admin}/AbrIT/Provisioning", "ProvisioningAdmin", "Index");
        Map(routes, "AbrIT.Admin.Provisioning.Mock", $"{admin}/AbrIT/Provisioning/MockFailures", "ProvisioningAdmin", "SetMockFailures");
    }

    private static void Map(IEndpointRouteBuilder routes, string name, string pattern, string controller, string action) =>
        routes.MapAreaControllerRoute(name, ModuleArea.Name, pattern, new { controller, action });
}
