using DistribuFlow.Modules.Catalog.Domain;
using DistribuFlow.Modules.Catalog.Search;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DistribuFlow.Modules.Catalog.Features;

public sealed record IndexProductCommand(string Name, string Description, string Category, decimal Price, List<string> Tags)
    : IRequest<Guid>;

public sealed class IndexProductValidator : AbstractValidator<IndexProductCommand>
{
    public IndexProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public sealed class IndexProductHandler(IProductSearchIndex index) : IRequestHandler<IndexProductCommand, Guid>
{
    public async Task<Guid> Handle(IndexProductCommand request, CancellationToken ct)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            Price = request.Price,
            Tags = request.Tags
        };
        await index.IndexAsync(product, ct);
        return product.Id;
    }
}

public static class IndexProductEndpoint
{
    public static void MapIndexProduct(this IEndpointRouteBuilder app) =>
        app.MapPost("/catalog/products", async (IndexProductCommand cmd, ISender sender) =>
        {
            var id = await sender.Send(cmd);
            return Results.Created($"/catalog/products/{id}", new { id });
        }).WithTags("Catalog");
}
