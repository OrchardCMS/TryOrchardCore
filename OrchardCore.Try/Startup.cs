using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Setup;
using System;

namespace OrchardCore.Try
{
    public class Startup : StartupBase
    {
        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSetup();
        }

        public override void Configure(IApplicationBuilder builder, IRouteBuilder routes, IServiceProvider serviceProvider)
        {
            routes.MapAreaRoute(
                name: "sites",
                areaName: "OrchardCore.Try",
                template: "sites/{action}",
                defaults: new { controller = "Home", action = "Index" }
            );
        }
    }
}
