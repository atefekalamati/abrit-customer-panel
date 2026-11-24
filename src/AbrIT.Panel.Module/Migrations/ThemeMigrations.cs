using OrchardCore.Data.Migration;
using OrchardCore.Recipes.Services;

namespace AbrIT.Panel.Module.Migrations;

/// <summary>
/// Moves sites created before phase E-1 (site theme = TheTheme, no localization) to AbrIT.Theme + fa-IR.
/// New sites get the same values from the setup recipe; running the step twice is harmless.
/// </summary>
public sealed class ThemeMigrations(IRecipeMigrator recipeMigrator) : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await recipeMigrator.ExecuteAsync("abrit-theme.recipe.json", this);
        return 1;
    }
}
