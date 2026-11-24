using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Module.Orders;
using AbrIT.Panel.Module.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace AbrIT.Panel.Module;

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
    }
}
