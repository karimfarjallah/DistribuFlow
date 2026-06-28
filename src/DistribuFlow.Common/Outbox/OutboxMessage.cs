namespace DistribuFlow.Common.Outbox;

/// <summary>
/// A domain/integration event persisted in the SAME transaction as the state change that produced it.
/// This is the transactional outbox: it guarantees we never lose an event between commit and publish.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = default!;        // assembly-qualified type name
    public string Content { get; set; } = default!;     // JSON payload
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedOnUtc { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}
