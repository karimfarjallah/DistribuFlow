using DistribuFlow.Common.Messaging;
using DistribuFlow.Common.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace DistribuFlow.Common;

public static class CommonExtensions
{
    /// <summary>Registers the in-process event bus (the swap point for a real broker).</summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, InProcessEventBus>();
        return services;
    }

    /// <summary>Starts an outbox dispatcher for a specific module DbContext.</summary>
    public static IServiceCollection AddOutboxProcessor<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddHostedService<OutboxProcessor<TContext>>();
        return services;
    }

    /// <summary>Maps the shared OutboxMessage table. Call from each module's OnModelCreating.</summary>
    public static void ConfigureOutbox(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages", schema);
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.Content).IsRequired();
            b.HasIndex(x => x.ProcessedOnUtc);
        });
    }
}
