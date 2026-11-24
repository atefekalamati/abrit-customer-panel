using System.Net;
using System.Text;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Nextcloud;
using Microsoft.Extensions.Logging.Abstractions;

namespace AbrIT.Panel.Domain.Tests.Provisioning;

/// <summary>Bonus §12 "reading status from the API": admin refresh stores a snapshot, never changes the order.</summary>
public class StatusRefreshTests
{
    private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request.Method + " " + request.RequestUri!.AbsolutePath);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Ocs(int code, string data = "[]") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"ocs\":{\"meta\":{\"statuscode\":" + code + "},\"data\":" + data + "}}", Encoding.UTF8, "application/json"),
    };

    private static OcsNextcloudProvisioner Ocs(ScriptedHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://cloud.example.test/") }, NullLogger<OcsNextcloudProvisioner>.Instance);

    [Fact]
    public async Task Mock_refresh_stores_in_memory_state()
    {
        var h = new Harness();
        var order = await h.PlaceAsync(plan: Harness.Team);
        await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(5);

        var outcome = await h.Provisioning.RefreshStatusAsync(order.OrderId, Harness.Admin);

        var snap = outcome.Request!.LastStatusSnapshot!;
        Assert.Equal(h.Clock.UtcNow, outcome.Request.LastCheckedUtc);
        Assert.Equal(ProvisionerMode.Mock, snap.Mode);
        Assert.True(snap.Found);
        Assert.True(snap.Enabled);
        Assert.Equal(500L * 1024 * 1024, snap.QuotaBytes);
        Assert.Equal(0, snap.UsedBytes);
        Assert.Equal(["abrit-team-1405-000001"], snap.Groups);
        Assert.Null(snap.ErrorCode);
        Assert.Equal(OrderStatus.Active, outcome.Order.Status);
        Assert.Same(outcome.Request, await h.Requests.GetByOrderIdAsync(order.OrderId));
    }

    [Fact]
    public async Task Real_refresh_reads_user_through_ocs()
    {
        const string user = """{"id":"abrit-ord1405-000001","enabled":true,"quota":{"quota":1073741824,"used":2048},"groups":[]}""";
        var reads = 0;
        var handler = new ScriptedHandler(r =>
            r.Method == HttpMethod.Get ? (Interlocked.Increment(ref reads) == 1 ? Ocs(404) : Ocs(100, user)) : Ocs(100));
        var h = new Harness(Ocs(handler));
        var order = await h.PlaceAsync();
        Assert.Equal(OrderStatus.Active, (await h.Approval.ApproveAsync(order.OrderId, Harness.Admin)).Status);
        handler.Calls.Clear();

        var outcome = await h.Provisioning.RefreshStatusAsync(order.OrderId, Harness.Admin);

        Assert.Equal(["GET /ocs/v1.php/cloud/users/abrit-ord1405-000001"], handler.Calls);
        var snap = outcome.Request!.LastStatusSnapshot!;
        Assert.Equal(ProvisionerMode.Real, snap.Mode);
        Assert.True(snap.Found);
        Assert.Equal(1_073_741_824, snap.QuotaBytes);
        Assert.Equal(2048, snap.UsedBytes);
    }

    [Fact]
    public async Task Real_refresh_failure_is_recorded_and_order_unchanged()
    {
        var failing = false;
        var reads = 0;
        var handler = new ScriptedHandler(r =>
            failing ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : r.Method == HttpMethod.Get && Interlocked.Increment(ref reads) == 1 ? Ocs(404) : Ocs(100));
        var h = new Harness(Ocs(handler));
        var order = await h.PlaceAsync();
        await h.Approval.ApproveAsync(order.OrderId, Harness.Admin);
        failing = true;

        var outcome = await h.Provisioning.RefreshStatusAsync(order.OrderId, Harness.Admin);

        Assert.Equal("NC_AUTH", outcome.Request!.LastStatusSnapshot!.ErrorCode);
        Assert.False(outcome.Request.LastStatusSnapshot.Found);
        Assert.Equal(OrderStatus.Active, outcome.Order.Status);
        Assert.Equal(ProvisioningStatus.Succeeded, outcome.Request.Status);
    }

    [Fact]
    public async Task Missing_account_is_reported_as_not_found()
    {
        var h = new Harness();
        var order = await h.PlaceAsync();
        h.Mock.FailNextCalls = 1;
        await h.Approval.ApproveAsync(order.OrderId, Harness.Admin); // fails before the account exists

        var outcome = await h.Provisioning.RefreshStatusAsync(order.OrderId, Harness.Admin);

        Assert.False(outcome.Request!.LastStatusSnapshot!.Found);
        Assert.Null(outcome.Request.LastStatusSnapshot.ErrorCode);
        Assert.Equal(OrderStatus.Failed, outcome.Order.Status);
    }

    [Fact]
    public async Task Refresh_requires_operator_and_skips_unprovisioned_orders()
    {
        var h = new Harness();
        var order = await h.PlaceAsync();

        await Assert.ThrowsAsync<OperatorNotAuthorizedException>(() => h.Provisioning.RefreshStatusAsync(order.OrderId, Harness.UserA));
        await Assert.ThrowsAsync<OrderNotFoundException>(() => h.Provisioning.RefreshStatusAsync("missing", Harness.Admin));
        var outcome = await h.Provisioning.RefreshStatusAsync(order.OrderId, Harness.Admin);

        Assert.Null(outcome.Request);
        Assert.Equal(OrderStatus.PendingPayment, outcome.Order.Status);
    }
}
