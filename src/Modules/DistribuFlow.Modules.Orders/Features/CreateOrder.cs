using DistribuFlow.Modules.Orders.Domain;
using DistribuFlow.Modules.Orders.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DistribuFlow.Modules.Orders.Features;

// --- Vertical slice: everything CreateOrder needs lives here ---

public sealed record CreateOrderLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
public sealed record CreateOrderCommand(string CustomerName, List<CreateOrderLine> Lines) : IRequest<Guid>;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.Quantity).GreaterThan(0);
            l.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
            l.RuleFor(i => i.ProductName).NotEmpty();
        });
    }
}

public sealed class CreateOrderHandler(OrdersDbContext db) : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var order = Order.Create(
            request.CustomerName,
            request.Lines.Select(l => new OrderLine
            {
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            }));

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);   // outbox interceptor writes OrderPlaced in the same tx
        return order.Id;
    }
}

public static class CreateOrderEndpoint
{
    public static void MapCreateOrder(this IEndpointRouteBuilder app) =>
        app.MapPost("/orders", async (CreateOrderCommand cmd, ISender sender) =>
        {
            var id = await sender.Send(cmd);
            return Results.Created($"/orders/{id}", new { id });
        }).WithTags("Orders");
}
