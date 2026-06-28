namespace DistribuFlow.Contracts;

/// <summary>Raised by Orders when a new order is placed. Drives the fulfillment saga.</summary>
public sealed record OrderPlacedIntegrationEvent(
    Guid OrderId,
    string CustomerName,
    IReadOnlyList<OrderLineDto> Lines,
    decimal Total) : IntegrationEvent;

/// <summary>Raised by Inventory when stock was successfully reserved for an order.</summary>
public sealed record StockReservedIntegrationEvent(Guid OrderId) : IntegrationEvent;

/// <summary>Raised by Inventory when stock could not be reserved (drives compensation).</summary>
public sealed record StockReservationFailedIntegrationEvent(Guid OrderId, string Reason) : IntegrationEvent;

/// <summary>Raised by Shipping when a shipment was created for an order.</summary>
public sealed record OrderShippedIntegrationEvent(Guid OrderId, Guid ShipmentId, string TrackingNumber) : IntegrationEvent;
