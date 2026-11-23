var builder = WebApplication.CreateBuilder(args);

// AutoSetup is configured exclusively through environment variables (see docker-compose.yml / .env.example).
builder.Services.AddOrchardCms(orchard => orchard.AddSetupFeatures("OrchardCore.AutoSetup"));

// Host-level liveness probe: independent of tenant setup state.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.UseStaticFiles();
app.UseOrchardCore();

app.Run();
