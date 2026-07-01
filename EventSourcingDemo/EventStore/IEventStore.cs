using EventSourcingDemo.Events;

namespace EventSourcingDemo.EventStore;

/// <summary>
/// Stores and retrieves domain events for an aggregate.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends new events to the store for a given aggregate.
    /// Uses optimistic concurrency: if the expected version doesn't match,
    /// a ConcurrencyException is thrown.
    /// </summary>
    Task SaveEventsAsync(Guid aggregateId, IEnumerable<DomainEvent> events, int expectedVersion);

    /// <summary>
    /// Loads all events for a given aggregate, in order.
    /// </summary>
    Task<List<DomainEvent>> GetEventsAsync(Guid aggregateId);
}
