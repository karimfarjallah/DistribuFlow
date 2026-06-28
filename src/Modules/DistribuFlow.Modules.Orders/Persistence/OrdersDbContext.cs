using DistribuFlow.Common;
using DistribuFlow.Modules.Orders.Domain;
using DistribuFlow.Modules.Orders.Saga;
using Microsoft.EntityFrameworkCore;

namespace DistribuFlow.Modules.Orders.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string Schema = "orders";

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderSagaState> SagaStates => Set<OrderSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.CustomerName).IsRequired().HasMaxLength(200);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(o => o.Total).HasPrecision(18, 2);
            b.HasMany(o => o.Lines).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLine>(b =>
        {
            b.ToTable("OrderLines");
            b.HasKey(l => l.Id);
            b.Property(l => l.ProductName).IsRequired().HasMaxLength(200);
            b.Property(l => l.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OrderSagaState>(b =>
        {
            b.ToTable("OrderSagaStates");
            b.HasKey(s => s.OrderId);
            b.Property(s => s.Status).HasConversion<string>().HasMaxLength(30);
        });

        modelBuilder.ConfigureOutbox(Schema);
    }
}
