using EventSourcingDemo.Events;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace EventSourcingDemo.EventStore;

/// <summary>
/// Event Store implementation backed by a SQL Server table.
///
/// Table schema:
///   CREATE TABLE Events (
///       Id              BIGINT IDENTITY(1,1) PRIMARY KEY,
///       AggregateId     UNIQUEIDENTIFIER NOT NULL,
///       Version         INT NOT NULL,
///       EventType       NVARCHAR(200) NOT NULL,
///       Data            NVARCHAR(MAX) NOT NULL,
///       Timestamp       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
///       CONSTRAINT UQ_Aggregate_Version UNIQUE (AggregateId, Version)
///   );
///   CREATE INDEX IX_Events_AggregateId ON Events (AggregateId, Version);
/// </summary>
public class SqlServerEventStore : IEventStore
{
    private readonly string _connectionString;

    public SqlServerEventStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task SaveEventsAsync(
        Guid aggregateId,
        IEnumerable<DomainEvent> events,
        int expectedVersion)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // All events from one command are saved atomically
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var @event in events)
            {
                expectedVersion++;

                var sql = @"
                    INSERT INTO Events (AggregateId, Version, EventType, Data)
                    VALUES (@AggregateId, @Version, @EventType, @Data)";

                using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@AggregateId", aggregateId);
                cmd.Parameters.AddWithValue("@Version", expectedVersion);
                cmd.Parameters.AddWithValue("@EventType", @event.GetType().Name);
                cmd.Parameters.AddWithValue("@Data",
                    JsonSerializer.Serialize(@event, @event.GetType()));

                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (SqlException ex) when (ex.Number == 2627) // Unique constraint violation
        {
            await transaction.RollbackAsync();
            throw new ConcurrencyException(
                $"Concurrency conflict on aggregate {aggregateId}. " +
                $"Expected version {expectedVersion - 1}, but another request has already " +
                $"written to version {expectedVersion}. Reload and retry.");
        }
    }

    public async Task<List<DomainEvent>> GetEventsAsync(Guid aggregateId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT EventType, Data
            FROM Events
            WHERE AggregateId = @AggregateId
            ORDER BY Version ASC";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@AggregateId", aggregateId);

        var events = new List<DomainEvent>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var eventType = reader.GetString(0);
            var data = reader.GetString(1);

            events.Add(DeserializeEvent(eventType, data));
        }

        return events;
    }

    private static DomainEvent DeserializeEvent(string eventType, string json)
    {
        return eventType switch
        {
            nameof(OrderPlaced)    => JsonSerializer.Deserialize<OrderPlaced>(json)!,
            nameof(OrderAccepted)  => JsonSerializer.Deserialize<OrderAccepted>(json)!,
            nameof(OrderRejected)  => JsonSerializer.Deserialize<OrderRejected>(json)!,
            nameof(OrderPickedUp)  => JsonSerializer.Deserialize<OrderPickedUp>(json)!,
            nameof(OrderDelivered) => JsonSerializer.Deserialize<OrderDelivered>(json)!,
            nameof(OrderCancelled) => JsonSerializer.Deserialize<OrderCancelled>(json)!,
            _ => throw new InvalidOperationException($"Unknown event type: {eventType}")
        };
    }
}
