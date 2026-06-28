using DistribuFlow.Common;
using DistribuFlow.Modules.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace DistribuFlow.Modules.Shipping.Persistence;

public sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : DbContext(options)
{
    public const string Schema = "shipping";
    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Shipment>(b =>
        {
            b.ToTable("Shipments");
            b.HasKey(s => s.Id);
            b.Property(s => s.TrackingNumber).IsRequired().HasMaxLength(50);
            b.Property(s => s.Status).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(s => s.OrderId);
        });
        modelBuilder.ConfigureOutbox(Schema);
    }
}
