using AbrIT.Panel.Domain.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace AbrIT.Panel.Nextcloud;

public static class NextcloudServiceCollectionExtensions
{
    /// <summary>Transient retries on top of the first attempt (AGENTS.md §6: at most one).</summary>
    public const int TransientRetries = 1;

    /// <summary>
    /// Registers the connector chosen by <see cref="NextcloudOptions.Mode"/> (Mock stays the default). Requires a
    /// <see cref="NextcloudOptions"/> singleton that has already passed <see cref="NextcloudOptions.ThrowIfInvalid"/>.
    /// Real mode: typed OCS client with a per-attempt timeout of <c>TimeoutSeconds</c> and one transient retry
    /// (5xx/408/429/network/timeout); the business retry stays the admin's controlled Retry.
    /// </summary>
    public static IServiceCollection AddNextcloudConnector(this IServiceCollection services)
    {
        services.AddSingleton(sp => new MockNextcloudProvisioner(sp.GetRequiredService<NextcloudOptions>().Mock.FailNextCalls));

        services.AddHttpClient<OcsNextcloudProvisioner>(OcsNextcloudProvisioner.HttpClientName, (sp, http) =>
            {
                var options = sp.GetRequiredService<NextcloudOptions>();
                if (options.Mode != ProvisionerMode.Real)
                {
                    return;
                }

                http.BaseAddress = new Uri(options.BaseUrl!.TrimEnd('/') + "/");
                http.DefaultRequestHeaders.Authorization = OcsNextcloudProvisioner.BasicAuth(options.AdminUser!, options.AppPassword!);

                // The resilience pipeline owns timeouts; this is only an outer safety net.
                http.Timeout = TimeSpan.FromSeconds((options.TimeoutSeconds * (TransientRetries + 1)) + 5);
            })
            .AddResilienceHandler("nextcloud", (pipeline, context) =>
            {
                var timeout = TimeSpan.FromSeconds(context.ServiceProvider.GetRequiredService<NextcloudOptions>().TimeoutSeconds);
                pipeline
                    .AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = TransientRetries,
                        BackoffType = DelayBackoffType.Constant,
                        Delay = TimeSpan.FromMilliseconds(500),
                        UseJitter = true,
                    })
                    .AddTimeout(timeout);
            });

        services.AddScoped<INextcloudProvisioner>(sp =>
            sp.GetRequiredService<NextcloudOptions>().Mode == ProvisionerMode.Real
                ? sp.GetRequiredService<OcsNextcloudProvisioner>()
                : sp.GetRequiredService<MockNextcloudProvisioner>());

        return services;
    }
}
