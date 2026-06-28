using DistribuFlow.Modules.Catalog.Features;
using DistribuFlow.Modules.Catalog.Search;
using DistribuFlow.Modules.Inventory.Domain;
using DistribuFlow.Modules.Inventory.Persistence;
using DistribuFlow.Modules.Orders.Persistence;
using DistribuFlow.Modules.Shipping.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DistribuFlow.Api;

/// <summary>
/// Demo bootstrap: creates each module's database (EnsureCreated — production uses migrations),
/// ensures the ES index exists, and seeds some stock + searchable products.
/// </summary>
public static class DbInitializer
{
    // Fixed product ids so the seeded catalog and seeded stock line up for the order demo.
    public static readonly Guid LaptopId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid MouseId  = Guid.Parse("11111111-0000-0000-0000-000000000002");
    public static readonly Guid DeskId   = Guid.Parse("11111111-0000-0000-0000-000000000003");

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // 1) Create databases (each module owns its own DB -> trivial to extract to a microservice later).
        //    Retry: SQL Server in Docker can take 20-40s to accept connections on a cold start.
        await WithRetryAsync(() => sp.GetRequiredService<OrdersDbContext>().Database.EnsureCreatedAsync());
        var inventory = sp.GetRequiredService<InventoryDbContext>();
        await WithRetryAsync(() => inventory.Database.EnsureCreatedAsync());
        await WithRetryAsync(() => sp.GetRequiredService<ShippingDbContext>().Database.EnsureCreatedAsync());

        // 2) Seed stock.
        if (!await inventory.StockItems.AnyAsync())
        {
            inventory.StockItems.AddRange(
                new StockItem { ProductId = LaptopId, ProductName = "Pro Laptop 16", QuantityOnHand = 25 },
                new StockItem { ProductId = MouseId,  ProductName = "Wireless Mouse", QuantityOnHand = 200 },
                new StockItem { ProductId = DeskId,   ProductName = "Standing Desk",  QuantityOnHand = 3 });
            await inventory.SaveChangesAsync();
        }

        // 3) Ensure ES index + seed searchable products (best-effort; don't crash if ES is still starting).
        try
        {
            var index = sp.GetRequiredService<IProductSearchIndex>();
            await index.EnsureCreatedAsync();

            var sender = sp.GetRequiredService<ISender>();
            await sender.Send(new IndexProductCommand("Pro Laptop 16", "Powerful developer laptop with 32GB RAM", "Computers", 2499m, new() { "laptop", "developer", "portable" }));
            await sender.Send(new IndexProductCommand("Wireless Mouse", "Ergonomic wireless mouse", "Accessories", 39m, new() { "mouse", "wireless", "ergonomic" }));
            await sender.Send(new IndexProductCommand("Standing Desk", "Electric height-adjustable standing desk", "Furniture", 599m, new() { "desk", "standing", "office" }));
            await sender.Send(new IndexProductCommand("Mechanical Keyboard", "Tactile mechanical keyboard with hot-swap switches", "Accessories", 129m, new() { "keyboard", "mechanical", "developer" }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[seed] Elasticsearch seeding skipped (is ES ready?): {ex.Message}");
        }
    }

    /// <summary>Retries a transient startup operation (e.g. SQL Server still booting in Docker).</summary>
    private static async Task WithRetryAsync(Func<Task> action, int maxAttempts = 12, int delaySeconds = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { await action(); return; }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                Console.WriteLine($"[init] DB not ready (attempt {attempt}/{maxAttempts}): {ex.Message}. Retrying in {delaySeconds}s...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }
}
