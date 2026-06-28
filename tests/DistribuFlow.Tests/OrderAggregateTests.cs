using DistribuFlow.Contracts;
using DistribuFlow.Modules.Orders.Domain;
using FluentAssertions;
using Xunit;

namespace DistribuFlow.Tests;

public class OrderAggregateTests
{
    private static OrderLine Line(int qty, decimal price) => new()
    {
        ProductId = Guid.NewGuid(),
        ProductName = "Widget",
        Quantity = qty,
        UnitPrice = price
    };

    [Fact]
    public void Create_computes_total_from_lines()
    {
        var order = Order.Create("Acme", new[] { Line(2, 10m), Line(1, 5m) });

        order.Total.Should().Be(25m);
        order.Status.Should().Be(OrderStatus.Pending);
        order.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void Create_raises_OrderPlaced_event()
    {
        var order = Order.Create("Acme", new[] { Line(1, 99m) });

        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderPlacedIntegrationEvent>();

        var placed = (OrderPlacedIntegrationEvent)order.DomainEvents.Single();
        placed.OrderId.Should().Be(order.Id);
        placed.Total.Should().Be(99m);
    }

    [Fact]
    public void Status_transitions_follow_the_happy_path()
    {
        var order = Order.Create("Acme", new[] { Line(1, 1m) });

        order.MarkStockReserved();
        order.Status.Should().Be(OrderStatus.StockReserved);

        order.MarkShipped();
        order.Complete();
        order.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public void Cancel_sets_cancelled_status()
    {
        var order = Order.Create("Acme", new[] { Line(1, 1m) });
        order.Cancel();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void ClearDomainEvents_empties_the_collection()
    {
        var order = Order.Create("Acme", new[] { Line(1, 1m) });
        order.ClearDomainEvents();
        order.DomainEvents.Should().BeEmpty();
    }
}
