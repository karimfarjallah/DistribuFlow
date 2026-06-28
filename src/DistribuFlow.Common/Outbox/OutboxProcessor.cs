using System.Text.Json;
using DistribuFlow.Common.Messaging;
using DistribuFlow.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DistribuFlow.Common.Outbox;

/// <summary>
/// Background service that polls a module's outbox and publishes unprocessed messages to the bus.
/// Generic over the module DbContext, so each module gets its own independent dispatcher.
/// At-least-once delivery: a message is only marked processed after a successful publish.
/// </summary>
public sealed class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started for {Context}", typeof(TContext).Name);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Outbox loop error for {Context}", typeof(TContext).Name); }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"Unknown event type: {message.Type}");
                var @event = (IIntegrationEvent)JsonSerializer.Deserialize(message.Content, type)!;

                await bus.PublishAsync(@event, ct);
                message.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;
                logger.LogError(ex, "Failed to publish outbox message {Id} (attempt {Attempts})",
                    message.Id, message.Attempts);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
