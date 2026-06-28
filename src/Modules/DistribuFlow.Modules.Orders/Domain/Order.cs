using DistribuFlow.Common.Domain;
using DistribuFlow.Contracts;

namespace DistribuFlow.Modules.Orders.Domain;

public enum OrderStatus { Pending, StockReserved, Shipped, Completed, Cancelled }

public class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string CustomerName { get; private set; } = default!;
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public decimal Total { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    private Order() { }   // EF

    public static Order Create(string customerName, IEnumerable<OrderLine> lines)
    {
        var order = new Order { CustomerName = customerName };
        order._lines.AddRange(lines);
        order.Total = order._lines.Sum(l => l.UnitPrice * l.Quantity);

        // Raised here, flushed to the outbox on SaveChanges -> drives the saga.
        order.Raise(new OrderPlacedIntegrationEvent(
            order.Id,
            order.CustomerName,
            order._lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList(),
            order.Total));
        return order;
    }

    public void MarkStockReserved() => Status = OrderStatus.StockReserved;
    public void MarkShipped() => Status = OrderStatus.Shipped;
    public void Complete() => Status = OrderStatus.Completed;
    public void Cancel() => Status = OrderStatus.Cancelled;
}

public class OrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
