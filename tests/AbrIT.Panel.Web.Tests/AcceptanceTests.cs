using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Payments;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Nextcloud;
using AbrIT.Panel.Web.Tests.Infrastructure;

namespace AbrIT.Panel.Web.Tests;

/// <summary>
/// Acceptance criteria AC-01..AC-08 (AGENTS.md §10) as real HTTP flows against the Orchard host: forms with
/// antiforgery tokens, one cookie jar per user. Tenant services are only read to assert, never to act.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed partial class AcceptanceTests(SharedPanelHost shared)
{
    private const long OneGiB = 1_073_741_824;
    private PanelHost Host => shared.Host;

    [Fact]
    public async Task AC01_Register_Login_Logout_ForgotPassword_Reset_LoginWithNewPassword()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"it-user-{suffix}";
        var email = $"{userName}@example.test";
        const string firstPassword = "It-First-Pa55!";
        const string newPassword = "It-Reset-Pa55!";
        using var client = new PanelClient(Host.CreatePanelClient());

        var register = await client.SubmitFormAsync("/Register", "/Register", new Dictionary<string, string>
        {
            ["UserName"] = userName,
            ["Email"] = email,
            ["Password"] = firstPassword,
            ["ConfirmPassword"] = firstPassword,
        });
        Assert.True(PanelClient.IsRedirect(register), $"register returned {(int)register.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/dashboard", followRedirects: false)).Status);

        var logOff = await client.SubmitFormAsync("/dashboard", "LogOff");
        Assert.True(PanelClient.IsRedirect(logOff), $"logoff returned {(int)logOff.StatusCode}");
        Assert.True(PanelClient.IsRedirect((await client.Http.GetAsync("/dashboard"))), "dashboard must require login after logoff");

        await client.LoginAsync(userName, firstPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/dashboard", followRedirects: false)).Status);
        await client.SubmitFormAsync("/dashboard", "LogOff");

        var sentBefore = Host.Emails.Messages.Count;
        var forgot = await client.SubmitFormAsync("/ForgotPassword", "/ForgotPassword", new Dictionary<string, string>
        {
            ["UsernameOrEmail"] = email,
        });
        Assert.True(PanelClient.IsRedirect(forgot), $"forgot password returned {(int)forgot.StatusCode}");

        var mail = Assert.Single(Host.Emails.Messages.Skip(sentBefore));
        Assert.Contains(email, mail.To);
        var link = ResetLinkRegex().Match(WebUtility.HtmlDecode(mail.HtmlBody ?? mail.TextBody ?? string.Empty));
        Assert.True(link.Success, "reset e-mail must contain a /ResetPassword link");
        var resetUrl = new Uri(link.Value).PathAndQuery;

        var reset = await client.SubmitFormAsync(resetUrl, "/ResetPassword", new Dictionary<string, string>
        {
            ["UsernameOrEmail"] = email,
            ["NewPassword"] = newPassword,
            ["PasswordConfirmation"] = newPassword,
        });
        Assert.True(PanelClient.IsRedirect(reset), $"reset returned {(int)reset.StatusCode}");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.LoginAsync(userName, firstPassword));
        await client.LoginAsync(userName, newPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/dashboard", followRedirects: false)).Status);
    }

    [Fact]
    public async Task AC02_CustomerA_OrdersStarter_IsPendingPayment_WithOwnerAndAmount()
    {
        using var customerA = await Host.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);

        var orderId = await PlaceStarterOrderAsync(customerA);

        var order = await Host.GetOrderAsync(orderId);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(PanelHost.CustomerA, order.OwnerUserName);
        Assert.False(string.IsNullOrEmpty(order.OwnerUserId));
        Assert.Equal("drive-starter", order.PlanSnapshot.PlanKey);
        Assert.Equal(99_000m, order.AmountToman);

        var page = await customerA.GetAsync($"/dashboard/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("در انتظار تأیید پرداخت", page.Text);
        Assert.Contains(order.OrderNumber, page.Text);
    }

    [Fact]
    public async Task AC03_AC04_AdminApproves_BecomesActiveMock_With1GiBQuota()
    {
        using var customerA = await Host.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);
        using var admin = await Host.LoginAsync(PanelHost.AdminUserName, PanelHost.AdminPassword);
        var orderId = await PlaceStarterOrderAsync(customerA);

        await ApproveAsync(admin, orderId);

        var order = await Host.GetOrderAsync(orderId);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Equal(PaymentStatus.ConfirmedSimulated, order.PaymentStatus);
        Assert.Equal(
            [OrderStatus.Paid, OrderStatus.Provisioning, OrderStatus.Active],
            order.History.Select(h => h.To).ToArray());

        var request = await Host.GetProvisioningRequestAsync(orderId);
        Assert.NotNull(request);
        Assert.Equal(ProvisioningStatus.Succeeded, request.Status);
        Assert.Equal(ProvisionerMode.Mock, request.Mode);
        Assert.Equal(OneGiB, request.QuotaBytes);

        var mock = await Host.GetMockConnectorAsync();
        var account = await mock.GetUserAsync(request.ExternalUserId, CancellationToken.None);
        Assert.NotNull(account);
        Assert.Equal(OneGiB, account.QuotaBytes);

        var page = await customerA.GetAsync($"/dashboard/orders/{orderId}");
        Assert.Contains("فعال آزمایشی", page.Text);
        Assert.Contains("(Mock)", page.Text);
        Assert.Contains(request.ExternalUserId, page.Text);
    }

    [Fact]
    public async Task AC05_DoubleApproveAndCallback_CreatesOneExternalAccount()
    {
        using var customerA = await Host.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);
        using var admin = await Host.LoginAsync(PanelHost.AdminUserName, PanelHost.AdminPassword);
        var orderId = await PlaceStarterOrderAsync(customerA);
        var mock = await Host.GetMockConnectorAsync();
        var createsBefore = mock.CreateUserCalls;

        await ApproveAsync(admin, orderId);
        var secondApprove = await admin.PostWithTokenAsync("/Admin/AbrIT/Orders", $"/Admin/AbrIT/Orders/{orderId}/ApprovePayment");
        Assert.True(PanelClient.IsRedirect(secondApprove), $"second approve returned {(int)secondApprove.StatusCode}");

        using var gateway = Host.CreatePanelClient();
        for (var i = 0; i < 2; i++)
        {
            var callback = await gateway.SendAsync(SignedCallback(orderId));
            Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
            Assert.Contains("Active", await callback.Content.ReadAsStringAsync());
        }

        var unsigned = new HttpRequestMessage(HttpMethod.Post, "/api/payments/mock-callback")
        {
            Content = new StringContent($$"""{"orderId":"{{orderId}}"}""", Encoding.UTF8, "application/json"),
        };
        Assert.Equal(HttpStatusCode.Unauthorized, (await gateway.SendAsync(unsigned)).StatusCode);

        Assert.Equal(1, mock.CreateUserCalls - createsBefore);
        var request = await Host.GetProvisioningRequestAsync(orderId);
        Assert.NotNull(request);
        Assert.Equal(1, request.Attempts);
        var order = await Host.GetOrderAsync(orderId);
        Assert.Equal(OrderStatus.Active, order!.Status);
        Assert.Single(order.History, h => h.To == OrderStatus.Active);
    }

    [Fact]
    public async Task AC06_ProvisioningFailure_SetsFailed_RetryActivates()
    {
        using var customerA = await Host.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);
        using var admin = await Host.LoginAsync(PanelHost.AdminUserName, PanelHost.AdminPassword);
        var orderId = await PlaceStarterOrderAsync(customerA);

        try
        {
            var inject = await admin.SubmitFormAsync("/Admin/AbrIT/Provisioning", "MockFailures", new Dictionary<string, string>
            {
                ["failNext"] = "1",
            });
            Assert.True(PanelClient.IsRedirect(inject), $"failure toggle returned {(int)inject.StatusCode}");

            await ApproveAsync(admin, orderId);

            var failed = await Host.GetOrderAsync(orderId);
            Assert.Equal(OrderStatus.Failed, failed!.Status);
            var failedRequest = await Host.GetProvisioningRequestAsync(orderId);
            Assert.Equal(ProvisioningStatus.Failed, failedRequest!.Status);
            Assert.Equal(MockNextcloudProvisioner.InjectedFailureCode, failedRequest.LastErrorCode);
            Assert.True(failedRequest.CanRetry);
            Assert.Contains("خطا در فعال‌سازی", (await customerA.GetAsync($"/dashboard/orders/{orderId}")).Text);

            var retry = await admin.SubmitFormAsync("/Admin/AbrIT/Orders", $"/{orderId}/Retry");
            Assert.True(PanelClient.IsRedirect(retry), $"retry returned {(int)retry.StatusCode}");

            var order = await Host.GetOrderAsync(orderId);
            Assert.Equal(OrderStatus.Active, order!.Status);
            var request = await Host.GetProvisioningRequestAsync(orderId);
            Assert.Equal(ProvisioningStatus.Succeeded, request!.Status);
            Assert.Equal(2, request.Attempts);
            Assert.Contains("فعال آزمایشی", (await customerA.GetAsync($"/dashboard/orders/{orderId}")).Text);
        }
        finally
        {
            (await Host.GetMockConnectorAsync()).FailNextCalls = 0;
        }
    }

    [Fact]
    public async Task AC07_CustomerB_GetsNotFound_OnCustomerAOrder()
    {
        using var customerA = await Host.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);
        using var customerB = await Host.LoginAsync(PanelHost.CustomerB, PanelHost.CustomerBPassword);
        var orderId = await PlaceStarterOrderAsync(customerA);
        var orderNumber = (await Host.GetOrderAsync(orderId))!.OrderNumber;

        Assert.Equal(HttpStatusCode.OK, (await customerA.GetAsync($"/dashboard/orders/{orderId}", followRedirects: false)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await customerB.GetAsync($"/dashboard/orders/{orderId}", followRedirects: false)).Status);

        var cancel = await customerB.PostWithTokenAsync(await OrderPageUrlAsync(customerB), $"/dashboard/orders/{orderId}/cancel");
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.DoesNotContain(orderNumber, (await customerB.GetAsync("/dashboard")).Text);
        Assert.Equal(OrderStatus.PendingPayment, (await Host.GetOrderAsync(orderId))!.Status);
    }

    [Fact]
    public async Task AC08_NonAdmin_ApprovePost_IsDenied_AndOrderUnchanged()
    {
        using var customerA = await Host.LoginAsync(PanelHost.CustomerA, PanelHost.CustomerAPassword);
        using var customerB = await Host.LoginAsync(PanelHost.CustomerB, PanelHost.CustomerBPassword);
        var orderId = await PlaceStarterOrderAsync(customerA);
        var mock = await Host.GetMockConnectorAsync();
        var createsBefore = mock.CreateUserCalls;

        // A valid antiforgery token for B, so the denial comes from authorization, not from token validation.
        var tokenPage = await OrderPageUrlAsync(customerB);
        foreach (var customer in new[] { customerA, customerB })
        {
            var approve = await customer.PostWithTokenAsync(tokenPage, $"/Admin/AbrIT/Orders/{orderId}/ApprovePayment");
            AssertDenied(approve);
            AssertDenied(await customer.Http.GetAsync("/Admin/AbrIT/Orders"));
        }

        // Anonymous without a token: rejected by antiforgery (400) or authorization; either way nothing happens.
        using var anonymous = Host.CreatePanelClient();
        var anonymousApprove = await anonymous.PostAsync($"/Admin/AbrIT/Orders/{orderId}/ApprovePayment", new FormUrlEncodedContent([]));
        if (anonymousApprove.StatusCode != HttpStatusCode.BadRequest)
        {
            AssertDenied(anonymousApprove);
        }

        var order = await Host.GetOrderAsync(orderId);
        Assert.Equal(OrderStatus.PendingPayment, order!.Status);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(await Host.GetProvisioningRequestAsync(orderId));
        Assert.Equal(createsBefore, mock.CreateUserCalls);
    }

    internal static async Task<string> PlaceStarterOrderAsync(PanelClient customer)
    {
        var drive = await customer.GetAsync("/services/drive");
        Assert.Equal(HttpStatusCode.OK, drive.Status);
        var planIds = PlanLinkRegex().Matches(drive.Html).Select(m => m.Groups["id"].Value).Distinct().ToList();
        Assert.NotEmpty(planIds);

        // Starter is the first orderable plan (SortOrder); AC-02 asserts the snapshot's PlanKey.
        var response = await customer.SubmitFormAsync($"/orders/new?plan={planIds[0]}", "/orders/new");
        Assert.True(PanelClient.IsRedirect(response), $"order POST returned {(int)response.StatusCode}");
        var location = response.Headers.Location!.OriginalString;
        var match = OrderLocationRegex().Match(location);
        Assert.True(match.Success, $"unexpected redirect after order: {location}");
        return match.Groups["id"].Value;
    }

    internal static async Task ApproveAsync(PanelClient admin, string orderId)
    {
        var response = await admin.SubmitFormAsync("/Admin/AbrIT/Orders", $"/{orderId}/ApprovePayment");
        Assert.True(PanelClient.IsRedirect(response), $"approve returned {(int)response.StatusCode}");
    }

    private static HttpRequestMessage SignedCallback(string orderId)
    {
        var body = Encoding.UTF8.GetBytes($$"""{"orderId":"{{orderId}}"}""");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/mock-callback")
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add(MockPaymentSignature.HeaderName, MockPaymentSignature.Compute(PanelHost.WebhookSecret, body));
        return request;
    }

    /// <summary>A page of the customer's own that carries a form (and therefore an antiforgery token).</summary>
    private static async Task<string> OrderPageUrlAsync(PanelClient customer)
    {
        var drive = await customer.GetAsync("/services/drive");
        return $"/orders/new?plan={PlanLinkRegex().Match(drive.Html).Groups["id"].Value}";
    }

    /// <summary>Forbidden, or redirected to login / access-denied — never a successful action.</summary>
    private static void AssertDenied(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return;
        }

        Assert.True(PanelClient.IsRedirect(response), $"expected denial, got {(int)response.StatusCode} for {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}");
        var location = response.Headers.Location!.OriginalString;
        Assert.Matches("(?i)(login|accessdenied|error/403)", location);
    }

    [GeneratedRegex(@"/orders/new\?plan=(?<id>[A-Za-z0-9_-]+)")]
    private static partial Regex PlanLinkRegex();

    [GeneratedRegex(@"/dashboard/orders/(?<id>[A-Za-z0-9_-]+)$")]
    private static partial Regex OrderLocationRegex();

    [GeneratedRegex(@"https?://[^""'\s<>]+/ResetPassword\?[^""'\s<>]+")]
    private static partial Regex ResetLinkRegex();
}
