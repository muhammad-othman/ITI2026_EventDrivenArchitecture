namespace EventSourcingDemo.EventStore;

/// <summary>
/// Thrown when two requests try to modify the same aggregate concurrently.
/// The second request detects that the version it loaded is stale.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}
