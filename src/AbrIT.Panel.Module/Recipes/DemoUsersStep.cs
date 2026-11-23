using AbrIT.Panel.Domain.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace AbrIT.Panel.Module.Recipes;

/// <summary>
/// Recipe step <c>DemoUsers</c>: seeds customer-a / customer-b from DEMO_CUSTOMER_*_PASSWORD.
/// Skips silently when a password is empty; idempotent (existing users are left untouched).
/// Passwords are never logged.
/// </summary>
public sealed class DemoUsersStep(
    UserManager<IUser> userManager,
    IConfiguration configuration,
    ILogger<DemoUsersStep> logger) : NamedRecipeStepHandler("DemoUsers")
{
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var spec in DemoUserSeeding.FromSettings(key => configuration[key]))
        {
            var exists = await userManager.FindByNameAsync(spec.UserName) is not null;
            if (DemoUserSeeding.Decide(spec, exists) != DemoUserAction.Create)
            {
                continue;
            }

            var user = new User
            {
                UserName = spec.UserName,
                Email = spec.Email,
                EmailConfirmed = true,
                IsEnabled = true,
            };

            var result = await userManager.CreateAsync(user, spec.Password!);
            if (result.Succeeded)
            {
                logger.LogInformation("Seeded demo user {UserName}.", spec.UserName);
            }
            else
            {
                // Identity error codes only; descriptions could echo policy details but never the password.
                logger.LogWarning(
                    "Could not seed demo user {UserName}: {ErrorCodes}.",
                    spec.UserName,
                    string.Join(", ", result.Errors.Select(e => e.Code)));
            }
        }
    }
}
