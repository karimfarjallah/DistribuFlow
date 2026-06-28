using DistribuFlow.Modules.Catalog.Domain;
using DistribuFlow.Modules.Catalog.Search;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DistribuFlow.Modules.Catalog.Features;

public sealed record SearchProductsQuery(string Q, int Size = 10) : IRequest<IReadOnlyList<Product>>;

/// <summary>Cache-aside read: try Redis, fall back to Elasticsearch, then populate the cache.</summary>
public sealed class SearchProductsHandler(IProductSearchIndex index, SearchCache cache)
    : IRequestHandler<SearchProductsQuery, IReadOnlyList<Product>>
{
    public async Task<IReadOnlyList<Product>> Handle(SearchProductsQuery request, CancellationToken ct)
    {
        var key = $"{request.Q}:{request.Size}";

        var cached = await cache.GetAsync(key);
        if (cached is not null) return cached;

        var results = await index.SearchAsync(request.Q, request.Size, ct);
        await cache.SetAsync(key, results);
        return results;
    }
}

public static class SearchProductsEndpoint
{
    public static void MapSearchProducts(this IEndpointRouteBuilder app) =>
        app.MapGet("/catalog/products/search", async (string q, ISender sender, int size = 10) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "q is required" });
            var results = await sender.Send(new SearchProductsQuery(q, size));
            return Results.Ok(results);
        }).WithTags("Catalog");
}
