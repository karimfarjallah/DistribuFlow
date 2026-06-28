using DistribuFlow.Common;
using DistribuFlow.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace DistribuFlow.Modules.Inventory.Persistence;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "inventory";

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockReservation> Reservations => Set<StockReservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StockItem>(b =>
        {
            b.ToTable("StockItems");
            b.HasKey(s => s.ProductId);
            b.Property(s => s.ProductName).IsRequired().HasMaxLength(200);
            b.Ignore(s => s.Available);
        });

        modelBuilder.Entity<StockReservation>(b =>
        {
            b.ToTable("StockReservations");
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.OrderId);
        });

        modelBuilder.ConfigureOutbox(Schema);
    }
}
