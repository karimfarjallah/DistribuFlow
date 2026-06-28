using DistribuFlow.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistribuFlow.Common.Messaging;

/// <summary>
/// In-process implementation of the event bus. Each publish runs handlers in a fresh DI scope,
/// so handler work (DbContexts, etc.) is isolated from the outbox dispatcher's scope.
/// SWAP POINT: replace with an Azure Service Bus / RabbitMQ publisher in production.
/// </summary>
public sealed class InProcessEventBus(IServiceScopeFactory scopeFactory, ILogger<InProcessEventBus> logger) : IEventBus
{
    public async Task PublishAsync(IIntegrationEvent @event, CancellationToken ct = default)
    {
        var eventType = @event.GetType();
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

        using var scope = scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType).Where(h => h is not null).ToList();

        if (handlers.Count == 0)
        {
            logger.LogDebug("No handlers for {EventType}", eventType.Name);
            return;
        }

        var method = handlerType.GetMethod("HandleAsync")!;
        foreach (var handler in handlers)
        {
            logger.LogInformation("Dispatching {EventType} -> {Handler}", eventType.Name, handler!.GetType().Name);
            await (Task)method.Invoke(handler, new object[] { @event, ct })!;
        }
    }
}
