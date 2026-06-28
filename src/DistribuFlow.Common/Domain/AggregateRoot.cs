using DistribuFlow.Contracts;

namespace DistribuFlow.Common.Domain;

/// <summary>Things that accumulate integration events to be flushed to the outbox on save.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IIntegrationEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

/// <summary>Base aggregate that records integration events raised during a unit of work.</summary>
public abstract class AggregateRoot : IHasDomainEvents
{
    private readonly List<IIntegrationEvent> _domainEvents = new();
    public IReadOnlyCollection<IIntegrationEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void Raise(IIntegrationEvent @event) => _domainEvents.Add(@event);

    /// <summary>
    /// Allows an application handler (not just the aggregate itself) to attach an integration
    /// event that should be flushed to the outbox on the next save. Used when the decision to
    /// raise an event lives in a command handler rather than inside the aggregate.
    /// </summary>
    public void RaisePublic(IIntegrationEvent @event) => _domainEvents.Add(@event);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
