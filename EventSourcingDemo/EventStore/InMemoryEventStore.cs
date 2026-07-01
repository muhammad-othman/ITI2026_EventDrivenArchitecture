using EventSourcingDemo.Events;
using System.Collections.Concurrent;

namespace EventSourcingDemo.EventStore;

/// <summary>
/// In-memory Event Store for development, demos, and labs.
/// Uses the same interface as the SQL Server implementation,
/// so you can swap between them by changing DI registration.
///
/// This implementation includes proper optimistic concurrency
/// using locks, matching the behavior of the SQL Server version.
/// </summary>
public class InMemoryEventStore : IEventStore
{
    // Thread-safe dictionary: AggregateId -> list of stored events
    private readonly ConcurrentDictionary<Guid, List<StoredEvent>> _store = new();
    private readonly object _lock = new();

    public Task SaveEventsAsync(
        Guid aggregateId,
        IEnumerable<DomainEvent> events,
        int expectedVersion)
    {
        lock (_lock)
        {
            var existingEvents = _store.GetOrAdd(aggregateId, _ => new List<StoredEvent>());

            // Optimistic concurrency check
            var currentVersion = existingEvents.Count - 1;
            if (currentVersion != expectedVersion)
            {
                throw new ConcurrencyException(
                    $"Concurrency conflict on aggregate {aggregateId}. " +
                    $"Expected version {expectedVersion}, but current version is {currentVersion}. " +
                    $"Reload and retry.");
            }

            foreach (var @event in events)
            {
                existingEvents.Add(new StoredEvent(
                    AggregateId: aggregateId,
                    Version: existingEvents.Count,
                    EventType: @event.GetType().Name,
                    Event: @event,
                    Timestamp: @event.OccurredAt
                ));
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<DomainEvent>> GetEventsAsync(Guid aggregateId)
    {
        if (_store.TryGetValue(aggregateId, out var storedEvents))
        {
            var events = storedEvents
                .OrderBy(e => e.Version)
                .Select(e => e.Event)
                .ToList();

            return Task.FromResult(events);
        }

        return Task.FromResult(new List<DomainEvent>());
    }

    /// <summary>
    /// Returns all events across all aggregates, in insertion order.
    /// Useful for building projections (CQRS read side).
    /// </summary>
    public List<DomainEvent> GetAllEvents()
    {
        lock (_lock)
        {
            return _store.Values
                .SelectMany(events => events)
                .OrderBy(e => e.Timestamp)
                .Select(e => e.Event)
                .ToList();
        }
    }

    private record StoredEvent(
        Guid AggregateId,
        int Version,
        string EventType,
        DomainEvent Event,
        DateTime Timestamp
    );
}
