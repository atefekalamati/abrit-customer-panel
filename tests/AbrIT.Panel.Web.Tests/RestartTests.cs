using System.Net;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Web.Tests.Infrastructure;

namespace AbrIT.Panel.Web.Tests;

/// <summary>AC-09: a second host on the same App_Data (SQLite file) sees the orders and statuses of the first.</summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RestartTests
{
    [Fact]
    public async Task AC09_RestartHost_SameAppData_OrderAndStatusPersist()
    {
        var appData = PanelHost.NewAppDataPath();
        try
        {
            string activeOrderId;
            string pendingOrderId;
            string activeOrderNumber;
            await using (var first = new PanelHost(appData))
            {
                await first.WaitUntilReadyAsync();
                using var customerA = await first.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);
                using var admin = await first.LoginAsync(PanelHost.AdminUserName, PanelHost.AdminPassword);
                activeOrderId = await AcceptanceTests.PlaceStarterOrderAsync(customerA);
                pendingOrderId = await AcceptanceTests.PlaceStarterOrderAsync(customerA);
                await AcceptanceTests.ApproveAsync(admin, activeOrderId);
                activeOrderNumber = (await first.GetOrderAsync(activeOrderId))!.OrderNumber;
                Assert.Equal(OrderStatus.Active, (await first.GetOrderAsync(activeOrderId))!.Status);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            await using var second = new PanelHost(appData);
            await second.WaitUntilReadyAsync();

            var active = await second.GetOrderAsync(activeOrderId);
            Assert.NotNull(active);
            Assert.Equal(OrderStatus.Active, active.Status);
            Assert.Equal(activeOrderNumber, active.OrderNumber);
            Assert.Equal(ProvisioningStatus.Succeeded, (await second.GetProvisioningRequestAsync(activeOrderId))!.Status);
            Assert.Equal(OrderStatus.PendingPayment, (await second.GetOrderAsync(pendingOrderId))!.Status);

            // Users and their ownership survive too: A logs in again and sees both orders.
            using var customerAAgain = await second.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);
            var page = await customerAAgain.GetAsync($"/dashboard/orders/{activeOrderId}", followRedirects: false);
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.Contains("فعال آزمایشی", page.Text);
            var dashboard = await customerAAgain.GetAsync("/dashboard");
            Assert.Contains(activeOrderNumber, dashboard.Text);
            Assert.Contains((await second.GetOrderAsync(pendingOrderId))!.OrderNumber, dashboard.Text);
        }
        finally
        {
            PanelHost.DeleteAppData(appData);
        }
    }
}
