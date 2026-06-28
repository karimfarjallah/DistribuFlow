using System.Text.Json;
using DistribuFlow.Common.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DistribuFlow.Common.Outbox;

/// <summary>
/// EF Core interceptor that, just before saving, collects integration events from tracked
/// aggregates and writes them as OutboxMessage rows in the same transaction.
/// </summary>
public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is not null) WriteOutbox(context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context is not null) WriteOutbox(context);
        return base.SavingChanges(eventData, result);
    }

    private static void WriteOutbox(DbContext context)
    {
        var aggregates = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var @event in aggregate.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Type = @event.GetType().AssemblyQualifiedName!,
                    Content = JsonSerializer.Serialize(@event, @event.GetType()),
                    OccurredOnUtc = @event.OccurredOnUtc
                });
            }
            aggregate.ClearDomainEvents();
        }
    }
}
