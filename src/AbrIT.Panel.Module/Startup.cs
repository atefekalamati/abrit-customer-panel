using AbrIT.Panel.Module.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace AbrIT.Panel.Module;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<DemoUsersStep>();
    }
}
