using System.Text.Json;
using DistribuFlow.Contracts;
using FluentAssertions;
using Xunit;

namespace DistribuFlow.Tests;

/// <summary>
/// The outbox serializes events to JSON and the processor deserializes them by type name.
/// This guards that round-trip for the events that drive the saga.
/// </summary>
public class OutboxSerializationTests
{
    [Fact]
    public void OrderPlaced_round_trips_through_json()
    {
        var original = new OrderPlacedIntegrationEvent(
            Guid.NewGuid(), "Acme",
            new[] { new OrderLineDto(Guid.NewGuid(), "Widget", 2, 10m) }, 20m);

        var typeName = original.GetType().AssemblyQualifiedName!;
        var json = JsonSerializer.Serialize(original, original.GetType());

        var type = Type.GetType(typeName)!;
        var restored = (OrderPlacedIntegrationEvent)JsonSerializer.Deserialize(json, type)!;

        restored.OrderId.Should().Be(original.OrderId);
        restored.CustomerName.Should().Be("Acme");
        restored.Total.Should().Be(20m);
        restored.Lines.Should().ContainSingle().Which.ProductName.Should().Be("Widget");
        restored.EventId.Should().Be(original.EventId);
    }

    [Fact]
    public void StockReservationFailed_round_trips_with_reason()
    {
        var original = new StockReservationFailedIntegrationEvent(Guid.NewGuid(), "out of stock");
        var json = JsonSerializer.Serialize(original, original.GetType());
        var restored = JsonSerializer.Deserialize<StockReservationFailedIntegrationEvent>(json)!;

        restored.OrderId.Should().Be(original.OrderId);
        restored.Reason.Should().Be("out of stock");
    }
}
