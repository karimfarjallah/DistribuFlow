using DistribuFlow.Common;
using DistribuFlow.Common.Outbox;
using DistribuFlow.Modules.Shipping.Persistence;
using DistribuFlow.Modules.Shipping.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DistribuFlow.Modules.Shipping;

public static class ShippingModule
{
    public static IServiceCollection AddShippingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ShippingDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("Shipping"))
             .AddInterceptors(new OutboxInterceptor()));
        services.AddOutboxProcessor<ShippingDbContext>();
        return services;
    }

    public static IEndpointRouteBuilder MapShippingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapListShipments();
        return app;
    }

    public static System.Reflection.Assembly Assembly => typeof(ShippingModule).Assembly;
}
