using DistribuFlow.Common.Domain;

namespace DistribuFlow.Modules.Shipping.Domain;

public enum ShipmentStatus { Created, Dispatched }

public class Shipment : AggregateRoot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string TrackingNumber { get; set; } = default!;
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Created;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
