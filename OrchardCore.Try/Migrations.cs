using System.Threading.Tasks;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Services;

namespace OrchardCore.Try;

/// <summary>
/// Switches an already-running site over to TheTryTheme. A fresh install gets the theme from the setup
/// recipe (saas.recipe.json), but a site set up before the theme existed never re-runs that recipe — so
/// this migration does the same job for the sites already out there.
/// <para>
/// Migrations run once per tenant, and OrchardCore.Try is enabled on the default tenant only, so this
/// runs there and nowhere else: the demo tenants keep their own themes.
/// </para>
/// </summary>
public sealed class Migrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly ShellSettings _shellSettings;

    public Migrations(IRecipeMigrator recipeMigrator, ShellSettings shellSettings)
    {
        _recipeMigrator = recipeMigrator;
        _shellSettings = shellSettings;
    }

    public async Task<int> CreateAsync()
    {
        // Only touch a tenant that is already running. During setup the shell is still Initializing and
        // the setup recipe is mid-flight: enabling a feature from here reloads the shell and abandons
        // the migrations queued behind it — the Users indexes are never created, and every login then
        // fails with "no such table: UserIndex". The setup recipe already selects the theme, so there is
        // nothing for this migration to do on a fresh install.
        if (_shellSettings.IsRunning())
        {
            // Resolved under the module's Migrations folder (not Recipes, which is for setup recipes).
            await _recipeMigrator.ExecuteAsync("theme.recipe.json", this);
        }

        return 1;
    }
}
