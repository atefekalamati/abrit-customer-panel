using AbrIT.Panel.Module;
using AbrIT.Panel.Nextcloud;

var builder = WebApplication.CreateBuilder(args);

// Connector + mock payment settings come from env vars / user-secrets only. Validated here so that an incomplete
// Real-mode configuration stops the app at startup (error lists missing keys, never values). Tenants inherit both.
var nextcloud = builder.Configuration.GetSection(NextcloudOptions.SectionName).Get<NextcloudOptions>() ?? new NextcloudOptions();
nextcloud.ThrowIfInvalid();
builder.Services.AddSingleton(nextcloud);
builder.Services.AddSingleton(
    builder.Configuration.GetSection(MockPaymentOptions.SectionName).Get<MockPaymentOptions>() ?? new MockPaymentOptions());

// AutoSetup is configured exclusively through environment variables (see docker-compose.yml / .env.example).
builder.Services.AddOrchardCms(orchard => orchard.AddSetupFeatures("OrchardCore.AutoSetup"));

// Host-level liveness probe: independent of tenant setup state.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.UseStaticFiles();
app.UseOrchardCore();

app.Run();
