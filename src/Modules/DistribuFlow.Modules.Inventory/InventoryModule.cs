using DistribuFlow.Common;
using DistribuFlow.Common.Outbox;
using DistribuFlow.Modules.Inventory.Persistence;
using DistribuFlow.Modules.Inventory.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DistribuFlow.Modules.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<InventoryDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("Inventory"))
             .AddInterceptors(new OutboxInterceptor()));
        services.AddOutboxProcessor<InventoryDbContext>();
        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapListStock();
        return app;
    }

    public static System.Reflection.Assembly Assembly => typeof(InventoryModule).Assembly;
}
