using DistribuFlow.Common.Messaging;
using DistribuFlow.Contracts;
using DistribuFlow.Modules.Orders.Features;
using DistribuFlow.Modules.Orders.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DistribuFlow.Modules.Orders.Saga;

/// <summary>
/// Orchestration saga coordinating the order-to-shipping flow across modules.
///
///   OrderPlaced  -> reserve stock        (Inventory)
///   StockReserved -> arrange shipping     (Shipping)
///   OrderShipped  -> complete order
///   StockReservationFailed -> CANCEL order (compensation)
///
/// Reacts to integration events (from the outbox) and sends commands via MediatR.
/// All handlers are idempotent via the persisted saga state.
/// </summary>
public sealed class OrderFulfillmentSaga(OrdersDbContext db, ISender sender, ILogger<OrderFulfillmentSaga> logger) :
    IIntegrationEventHandler<OrderPlacedIntegrationEvent>,
    IIntegrationEventHandler<StockReservedIntegrationEvent>,
    IIntegrationEventHandler<StockReservationFailedIntegrationEvent>,
    IIntegrationEventHandler<OrderShippedIntegrationEvent>
{
    public async Task HandleAsync(OrderPlacedIntegrationEvent e, CancellationToken ct)
    {
        if (await db.SagaStates.FindAsync([e.OrderId], ct) is not null) return; // already started (idempotent)

        db.SagaStates.Add(new OrderSagaState { OrderId = e.OrderId, Status = SagaStatus.AwaitingStock });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[SAGA] Order {OrderId} placed -> reserving stock", e.OrderId);
        await sender.Send(new ReserveStockCommand(e.OrderId, e.Lines), ct);
    }

    public async Task HandleAsync(StockReservedIntegrationEvent e, CancellationToken ct)
    {
        var saga = await db.SagaStates.FindAsync([e.OrderId], ct);
        if (saga is null || saga.Status != SagaStatus.AwaitingStock) return;

        saga.Status = SagaStatus.AwaitingShipping;
        saga.UpdatedUtc = DateTime.UtcNow;
        await sender.Send(new MarkOrderStockReservedCommand(e.OrderId), ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[SAGA] Order {OrderId} stock reserved -> arranging shipping", e.OrderId);
        await sender.Send(new ArrangeShippingCommand(e.OrderId), ct);
    }

    public async Task HandleAsync(OrderShippedIntegrationEvent e, CancellationToken ct)
    {
        var saga = await db.SagaStates.FindAsync([e.OrderId], ct);
        if (saga is null || saga.Status != SagaStatus.AwaitingShipping) return;

        saga.Status = SagaStatus.Completed;
        saga.UpdatedUtc = DateTime.UtcNow;
        await sender.Send(new CompleteOrderCommand(e.OrderId), ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[SAGA] Order {OrderId} shipped ({Tracking}) -> COMPLETED", e.OrderId, e.TrackingNumber);
    }

    // --- Compensation path ---
    public async Task HandleAsync(StockReservationFailedIntegrationEvent e, CancellationToken ct)
    {
        var saga = await db.SagaStates.FindAsync([e.OrderId], ct);
        if (saga is null || saga.Status is SagaStatus.Cancelled or SagaStatus.Completed) return;

        saga.Status = SagaStatus.Compensating;
        saga.LastError = e.Reason;
        await db.SaveChangesAsync(ct);

        logger.LogWarning("[SAGA] Order {OrderId} stock FAILED ({Reason}) -> compensating (cancel order)", e.OrderId, e.Reason);
        await sender.Send(new CancelOrderCommand(e.OrderId, e.Reason), ct);

        saga.Status = SagaStatus.Cancelled;
        saga.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
