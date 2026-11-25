using AbrIT.Panel.Module.Security;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace AbrIT.Panel.Module;

public sealed class AdminMenu(IStringLocalizer<AdminMenu> localizer) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = localizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["ابریت"], "1", abrit => abrit
            .Id("abrit")
            .Add(S["سفارش‌ها"], "1", orders => orders
                .Action("Index", "OrdersAdmin", new { area = ModuleArea.Name })
                .Permission(AbrPermissions.ViewAllOrders)
                .LocalNav())
            .Add(S["تنظیمات اتصال Drive"], "2", provisioning => provisioning
                .Action("Index", "ProvisioningAdmin", new { area = ModuleArea.Name })
                .Permission(AbrPermissions.ManageProvisioning)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

public static class ModuleArea
{
    public const string Name = "AbrIT.Panel.Module";
}
