using System.Collections.Concurrent;
using AbrIT.Panel.Domain.Orders;
using AbrIT.Panel.Domain.Provisioning;
using AbrIT.Panel.Nextcloud;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using StartupBase = OrchardCore.Modules.StartupBase;

namespace AbrIT.Panel.Web.Tests.Infrastructure;

/// <summary>
/// The real Orchard host (Program.cs) on a temp App_Data: AutoSetup runs the AbrITPanel recipe with throwaway
/// credentials, demo customers A/B are seeded, Nextcloud is Mock, the mock payment callback is enabled.
/// Orchard reads App_Data from the process-wide ORCHARD_APP_DATA variable, hence <see cref="IntegrationCollection"/>
/// runs integration tests serially.
/// </summary>
public sealed class PanelHost(string appDataPath) : WebApplicationFactory<Program>
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "It-Admin-Pa55!";
    public const string CustomerA = "customer-a";
    public const string CustomerB = "customer-b";
    public const string CustomerAPassword = "It-CustA-Pa55!";
    public const string CustomerBPassword = "It-CustB-Pa55!";
    public const string WebhookSecret = "integration-test-webhook-secret";

    public string AppDataPath { get; } = appDataPath;

    /// <summary>Every e-mail the tenant sends (replaces Orchard's <see cref="IEmailService"/>).</summary>
    public EmailCapture Emails { get; } = new();

    public static string NewAppDataPath() =>
        Path.Combine(Path.GetTempPath(), "abrit-it", Guid.NewGuid().ToString("N")[..12]);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ORCHARD_APP_DATA", AppDataPath);

        const string auto = "OrchardCore:OrchardCore_AutoSetup:";
        const string tenant = auto + "Tenants:0:";
        // Production = precompiled views, as in Docker (Development switches Orchard to Razor runtime compilation).
        builder.UseEnvironment("Production");
        builder.UseSetting(auto + "AutoSetupPath", string.Empty);
        builder.UseSetting(tenant + "ShellName", "Default");
        builder.UseSetting(tenant + "SiteName", "AbrIT Panel (Integration)");
        builder.UseSetting(tenant + "SiteTimeZone", "Asia/Tehran");
        builder.UseSetting(tenant + "AdminUsername", AdminUserName);
        builder.UseSetting(tenant + "AdminEmail", "admin@example.test");
        builder.UseSetting(tenant + "AdminPassword", AdminPassword);
        builder.UseSetting(tenant + "DatabaseProvider", "Sqlite");
        builder.UseSetting(tenant + "RecipeName", "AbrITPanel");
        builder.UseSetting("DEMO_CUSTOMER_A_PASSWORD", CustomerAPassword);
        builder.UseSetting("DEMO_CUSTOMER_B_PASSWORD", CustomerBPassword);
        builder.UseSetting("Nextcloud:Mode", "Mock");
        builder.UseSetting("MockPayment:WebhookSecret", WebhookSecret);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            // Host descriptors are cloned into tenant containers; this IStartup runs after every module startup.
            services.AddSingleton(Emails);
            services.AddSingleton<OrchardCore.Modules.IStartup, EmailCaptureStartup>();
        });
    }

    public HttpClient CreatePanelClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public async Task<PanelClient> LoginAsync(string userName, string password)
    {
        var client = new PanelClient(CreatePanelClient());
        await client.LoginAsync(userName, password);
        return client;
    }

    /// <summary>Runs <paramref name="action"/> in a scope of the Default tenant (assertions only, never to act).</summary>
    public async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        var shellHost = Services.GetRequiredService<IShellHost>();
        var scope = await shellHost.GetScopeAsync(ShellSettings.DefaultShellName);
        T result = default!;
        await scope.UsingAsync(async s => result = await action(s.ServiceProvider));
        return result;
    }

    public Task<Order?> GetOrderAsync(string orderId) =>
        InTenantAsync(sp => sp.GetRequiredService<IOrderRepository>().GetAsync(orderId));

    public Task<ProvisioningRequest?> GetProvisioningRequestAsync(string orderId) =>
        InTenantAsync(sp => sp.GetRequiredService<IProvisioningRequestRepository>().GetByOrderIdAsync(orderId));

    public Task<MockNextcloudProvisioner> GetMockConnectorAsync() =>
        InTenantAsync(sp => Task.FromResult(sp.GetRequiredService<MockNextcloudProvisioner>()));

    public static void DeleteAppData(string path)
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a lingering file handle on Windows must not fail the run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Test-only tenant startup: swaps Orchard's e-mail service for <see cref="EmailCapture"/>.</summary>
public sealed class EmailCaptureStartup(EmailCapture capture) : StartupBase
{
    public override int Order => int.MaxValue;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IEmailService>();
        services.AddSingleton<IEmailService>(capture);
    }
}

public sealed class EmailCapture : IEmailService
{
    private readonly ConcurrentQueue<MailMessage> _messages = new();

    public IReadOnlyList<MailMessage> Messages => [.. _messages];

    public Task<Result> SendAsync(MailMessage message, string providerName = null!, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);
        return Task.FromResult(Result.Success());
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationCollection : ICollectionFixture<SharedPanelHost>
{
    public const string Name = "Integration";
}

/// <summary>One set-up host shared by AC-01..AC-08 (AutoSetup is the slow part); each test creates its own orders.</summary>
public sealed class SharedPanelHost : IAsyncLifetime
{
    public PanelHost Host { get; } = new(PanelHost.NewAppDataPath());

    public async Task InitializeAsync() => await Host.WaitUntilReadyAsync();

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        PanelHost.DeleteAppData(Host.AppDataPath);
    }
}

public static class PanelHostExtensions
{
    /// <summary>Starts the host and waits until AutoSetup has installed the tenant and the catalog is served.</summary>
    public static async Task WaitUntilReadyAsync(this PanelHost host)
    {
        using var client = host.CreatePanelClient();
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (true)
        {
            var response = await client.GetAsync("/services");
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && body.Contains("/services/drive", StringComparison.Ordinal))
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Tenant not ready: last /services status {(int)response.StatusCode}: {body[..Math.Min(body.Length, 3000)]}");
            }

            await Task.Delay(500);
        }
    }
}
