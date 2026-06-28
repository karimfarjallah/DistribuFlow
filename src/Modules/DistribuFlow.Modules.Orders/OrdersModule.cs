using DistribuFlow.Common;
using DistribuFlow.Common.Messaging;
using DistribuFlow.Common.Outbox;
using DistribuFlow.Contracts;
using DistribuFlow.Modules.Orders.Features;
using DistribuFlow.Modules.Orders.Persistence;
using DistribuFlow.Modules.Orders.Saga;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DistribuFlow.Modules.Orders;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OrdersDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("Orders"))
             .AddInterceptors(new OutboxInterceptor()));

        services.AddOutboxProcessor<OrdersDbContext>();

        // Register the saga and bind its four handled events to the same instance.
        services.AddScoped<OrderFulfillmentSaga>();
        services.AddScoped<IIntegrationEventHandler<OrderPlacedIntegrationEvent>>(sp => sp.GetRequiredService<OrderFulfillmentSaga>());
        services.AddScoped<IIntegrationEventHandler<StockReservedIntegrationEvent>>(sp => sp.GetRequiredService<OrderFulfillmentSaga>());
        services.AddScoped<IIntegrationEventHandler<StockReservationFailedIntegrationEvent>>(sp => sp.GetRequiredService<OrderFulfillmentSaga>());
        services.AddScoped<IIntegrationEventHandler<OrderShippedIntegrationEvent>>(sp => sp.GetRequiredService<OrderFulfillmentSaga>());

        return services;
    }

    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCreateOrder();
        app.MapGetOrder();
        return app;
    }

    /// <summary>The assembly, so the host can register MediatR/validators from it.</summary>
    public static System.Reflection.Assembly Assembly => typeof(OrdersModule).Assembly;
}
