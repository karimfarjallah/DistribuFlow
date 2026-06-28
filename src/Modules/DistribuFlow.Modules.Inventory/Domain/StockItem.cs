using DistribuFlow.Common.Domain;
using DistribuFlow.Contracts;

namespace DistribuFlow.Modules.Inventory.Domain;

/// <summary>Stock for a product. Aggregate raises StockReserved / StockReservationFailed.</summary>
public class StockItem : AggregateRoot
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int Available => QuantityOnHand - QuantityReserved;
}

public class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
