using DistribuFlow.Contracts;
using DistribuFlow.Modules.Inventory.Domain;
using DistribuFlow.Modules.Inventory.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DistribuFlow.Modules.Inventory.Features;

/// <summary>
/// Handles the saga's ReserveStockCommand. Idempotent: if a reservation already exists for the
/// order, it re-raises StockReserved and returns. Otherwise it checks availability across all
/// lines, and either reserves everything (StockReserved) or nothing (StockReservationFailed).
/// </summary>
public sealed class ReserveStockHandler(InventoryDbContext db, ILogger<ReserveStockHandler> logger)
    : IRequestHandler<ReserveStockCommand>
{
    public async Task Handle(ReserveStockCommand request, CancellationToken ct)
    {
        var already = await db.Reservations.AnyAsync(r => r.OrderId == request.OrderId, ct);
        if (already)
        {
            await RaiseOnAnyStockItem(request.OrderId, ok: true, reason: null, ct);
            return;
        }

        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var stock = await db.StockItems
            .Where(s => productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, ct);

        // Validate availability for every line first (all-or-nothing).
        foreach (var line in request.Lines)
        {
            if (!stock.TryGetValue(line.ProductId, out var item) || item.Available < line.Quantity)
            {
                var reason = !stock.ContainsKey(line.ProductId)
                    ? $"Unknown product {line.ProductId}"
                    : $"Insufficient stock for {line.ProductName} (need {line.Quantity}, have {stock[line.ProductId].Available})";

                logger.LogWarning("Stock reservation failed for order {OrderId}: {Reason}", request.OrderId, reason);
                await RaiseOnAnyStockItem(request.OrderId, ok: false, reason, ct);
                return;
            }
        }

        // Reserve.
        foreach (var line in request.Lines)
        {
            var item = stock[line.ProductId];
            item.QuantityReserved += line.Quantity;
            db.Reservations.Add(new StockReservation
            {
                OrderId = request.OrderId,
                ProductId = line.ProductId,
                Quantity = line.Quantity
            });
        }

        // Raise the success event on one of the touched aggregates (so the outbox picks it up).
        var carrier = stock[request.Lines[0].ProductId];
        carrier.RaisePublic(new StockReservedIntegrationEvent(request.OrderId));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Reserved stock for order {OrderId}", request.OrderId);
    }

    // Helper: attach an event to a tracked StockItem so the outbox interceptor serializes it.
    private async Task RaiseOnAnyStockItem(Guid orderId, bool ok, string? reason, CancellationToken ct)
    {
        var item = await db.StockItems.FirstAsync(ct);
        if (ok) item.RaisePublic(new StockReservedIntegrationEvent(orderId));
        else item.RaisePublic(new StockReservationFailedIntegrationEvent(orderId, reason ?? "unknown"));
        await db.SaveChangesAsync(ct);
    }
}
