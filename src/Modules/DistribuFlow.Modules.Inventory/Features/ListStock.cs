using DistribuFlow.Modules.Inventory.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DistribuFlow.Modules.Inventory.Features;

public static class ListStockEndpoint
{
    public static void MapListStock(this IEndpointRouteBuilder app) =>
        app.MapGet("/inventory/stock", async (InventoryDbContext db) =>
        {
            var stock = await db.StockItems.AsNoTracking()
                .Select(s => new { s.ProductId, s.ProductName, s.QuantityOnHand, s.QuantityReserved, Available = s.QuantityOnHand - s.QuantityReserved })
                .ToListAsync();
            return Results.Ok(stock);
        }).WithTags("Inventory");
}
