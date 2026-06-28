using DistribuFlow.Modules.Shipping.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DistribuFlow.Modules.Shipping.Features;

public static class ListShipmentsEndpoint
{
    public static void MapListShipments(this IEndpointRouteBuilder app) =>
        app.MapGet("/shipping/shipments", async (ShippingDbContext db) =>
            Results.Ok(await db.Shipments.AsNoTracking()
                .OrderByDescending(s => s.CreatedUtc).ToListAsync()))
        .WithTags("Shipping");
}
