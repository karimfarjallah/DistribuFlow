using DistribuFlow.Contracts;

namespace DistribuFlow.Common.Messaging;

/// <summary>Publishes integration events to all registered handlers.</summary>
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent @event, CancellationToken ct = default);
}

/// <summary>Handles a specific integration event type. Implementations must be idempotent.</summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
