namespace DistribuFlow.Modules.Orders.Saga;

public enum SagaStatus { Started, AwaitingStock, AwaitingShipping, Completed, Compensating, Cancelled }

/// <summary>Persisted state of the order-fulfillment saga, one row per order.</summary>
public class OrderSagaState
{
    public Guid OrderId { get; set; }
    public SagaStatus Status { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? LastError { get; set; }
}
