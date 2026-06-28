using DistribuFlow.Modules.Orders.Persistence;
using MediatR;

namespace DistribuFlow.Modules.Orders.Features;

// Internal commands the saga sends back into Orders (same module).
public sealed record CompleteOrderCommand(Guid OrderId) : IRequest;
public sealed record CancelOrderCommand(Guid OrderId, string Reason) : IRequest;
public sealed record MarkOrderStockReservedCommand(Guid OrderId) : IRequest;

public sealed class OrderStatusHandlers(OrdersDbContext db) :
    IRequestHandler<CompleteOrderCommand>,
    IRequestHandler<CancelOrderCommand>,
    IRequestHandler<MarkOrderStockReservedCommand>
{
    public async Task Handle(MarkOrderStockReservedCommand request, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([request.OrderId], ct);
        if (order is null) return;
        order.MarkStockReserved();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CompleteOrderCommand request, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([request.OrderId], ct);
        if (order is null) return;
        order.MarkShipped();
        order.Complete();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([request.OrderId], ct);
        if (order is null) return;
        order.Cancel();
        await db.SaveChangesAsync(ct);
    }
}
