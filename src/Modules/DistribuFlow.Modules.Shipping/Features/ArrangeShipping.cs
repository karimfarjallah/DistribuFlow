using DistribuFlow.Contracts;
using DistribuFlow.Modules.Shipping.Domain;
using DistribuFlow.Modules.Shipping.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DistribuFlow.Modules.Shipping.Features;

/// <summary>Handles the saga's ArrangeShippingCommand. Idempotent per order; raises OrderShipped.</summary>
public sealed class ArrangeShippingHandler(ShippingDbContext db, ILogger<ArrangeShippingHandler> logger)
    : IRequestHandler<ArrangeShippingCommand>
{
    public async Task Handle(ArrangeShippingCommand request, CancellationToken ct)
    {
        var existing = await db.Shipments.FirstOrDefaultAsync(s => s.OrderId == request.OrderId, ct);
        if (existing is not null)
        {
            existing.RaisePublic(new OrderShippedIntegrationEvent(request.OrderId, existing.Id, existing.TrackingNumber));
            await db.SaveChangesAsync(ct);
            return;
        }

        var shipment = new Shipment
        {
            OrderId = request.OrderId,
            TrackingNumber = $"TRK-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}"
        };
        shipment.RaisePublic(new OrderShippedIntegrationEvent(request.OrderId, shipment.Id, shipment.TrackingNumber));
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Shipment {Tracking} created for order {OrderId}", shipment.TrackingNumber, request.OrderId);
    }
}
