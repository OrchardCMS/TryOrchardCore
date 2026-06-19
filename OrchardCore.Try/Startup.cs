using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Setup;
using OrchardCore.Try.Services;
using System;

namespace OrchardCore.Try;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSetup();
        services.AddHostedService<DisableTenantsBackgroundService>();
    }

    public override void Configure(IApplicationBuilder builder, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "sites",
            areaName: "OrchardCore.Try",
            pattern: "sites/{action}",
            defaults: new { controller = "Home", action = "Index" }
        );
    }
}
