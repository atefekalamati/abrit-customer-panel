using System.Text.Encodings.Web;
using System.Text.Unicode;
using AbrIT.Panel.Module;
using AbrIT.Panel.Nextcloud;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// Connector + mock payment settings come from env vars / user-secrets only. Validated here so that an incomplete
// Real-mode configuration stops the app at startup (error lists missing keys, never values). Tenants inherit both.
var nextcloud = builder.Configuration.GetSection(NextcloudOptions.SectionName).Get<NextcloudOptions>() ?? new NextcloudOptions();
nextcloud.ThrowIfInvalid();
builder.Services.AddSingleton(nextcloud);
builder.Services.AddSingleton(
    builder.Configuration.GetSection(MockPaymentOptions.SectionName).Get<MockPaymentOptions>() ?? new MockPaymentOptions());

// E-mail (password reset etc.): Docker wires Orchard's config-based "DefaultSMTP" provider to smtp4dev through
// OrchardCore__OrchardCore_Email_Smtp__*. Without a configured SMTP host (local `dotnet run`), messages are written
// as .eml files to App_Data/mail via SMTP pickup-directory delivery, so a reset link can be verified without Docker.
const string smtp = "OrchardCore:OrchardCore_Email_Smtp:";
if (string.IsNullOrWhiteSpace(builder.Configuration[smtp + "Host"]))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        [smtp + "DefaultSender"] = builder.Configuration[smtp + "DefaultSender"] ?? "no-reply@abrit-panel.test",
        [smtp + "DeliveryMethod"] = "SpecifiedPickupDirectory",
        [smtp + "PickupDirectoryLocationBase"] = "{{ AppData }}/mail",
        [smtp + "PickupDirectoryLocation"] = "/",
    });
}

// Razor encodes every non-ASCII char as &#x..; by default. Allow all Unicode ranges so Persian is emitted as
// readable UTF-8 (HTML-sensitive chars such as < > & " are still encoded). Tenants inherit host services.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// AutoSetup is configured exclusively through environment variables (see docker-compose.yml / .env.example).
builder.Services.AddOrchardCms(orchard => orchard.AddSetupFeatures("OrchardCore.AutoSetup"));

// Host-level liveness probe: independent of tenant setup state.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.UseStaticFiles();
app.UseOrchardCore();

app.Run();
