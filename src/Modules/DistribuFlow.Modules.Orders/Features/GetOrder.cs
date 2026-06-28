using DistribuFlow.Modules.Orders.Persistence;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DistribuFlow.Modules.Orders.Features;

public sealed record OrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
public sealed record OrderDto(Guid Id, string CustomerName, string Status, decimal Total, DateTime CreatedUtc, List<OrderLineDto> Lines);
public sealed record GetOrderQuery(Guid Id) : IRequest<OrderDto?>;

public sealed class GetOrderHandler(OrdersDbContext db) : IRequestHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken ct) =>
        await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == request.Id)
            .Select(o => new OrderDto(
                o.Id, o.CustomerName, o.Status.ToString(), o.Total, o.CreatedUtc,
                o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList()))
            .FirstOrDefaultAsync(ct);
}

public static class GetOrderEndpoint
{
    public static void MapGetOrder(this IEndpointRouteBuilder app) =>
        app.MapGet("/orders/{id:guid}", async (Guid id, ISender sender) =>
        {
            var order = await sender.Send(new GetOrderQuery(id));
            return order is null ? Results.NotFound() : Results.Ok(order);
        }).WithTags("Orders");
}
