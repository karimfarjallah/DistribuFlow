using MediatR;

namespace DistribuFlow.Contracts;

/// <summary>Saga -> Inventory: reserve stock for the order's lines.</summary>
public sealed record ReserveStockCommand(Guid OrderId, IReadOnlyList<OrderLineDto> Lines) : IRequest;

/// <summary>Saga -> Shipping: arrange shipping for an order whose stock is reserved.</summary>
public sealed record ArrangeShippingCommand(Guid OrderId) : IRequest;
