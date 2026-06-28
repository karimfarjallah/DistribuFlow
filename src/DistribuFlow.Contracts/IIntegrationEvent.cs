namespace DistribuFlow.Contracts;

/// <summary>
/// Marker for events published across module boundaries via the (outbox-backed) event bus.
/// In production this is what would be serialized onto Azure Service Bus / RabbitMQ.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>A line on an order, shared between modules.</summary>
public sealed record OrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
