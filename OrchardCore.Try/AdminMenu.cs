using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;
using OrchardCore.Try.Controllers;
using System;
using System.Threading.Tasks;

namespace OrchardCore.Try;

public sealed class AdminMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public AdminMenu(IStringLocalizer<AdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.CompletedTask;
        }

        builder.Add(S["Multi-Tenancy"], tenancy => tenancy
            .Add(S["Test Tenants"], S["Test Tenants"].PrefixPosition(), item => item
                .Action("Index", "Admin", "OrchardCore.Try")
                .Permission(AdminController.ManageTenantsPermission)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
