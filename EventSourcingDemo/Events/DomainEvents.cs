namespace EventSourcingDemo.Events;

// ═══════════════════════════════════════════════════════════
// BASE EVENT
// Every domain event carries an ID and a timestamp.
// ═══════════════════════════════════════════════════════════

public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════
// ORDER LIFECYCLE EVENTS
// These are the DOMAIN events stored in the Event Store.
// They represent every meaningful state change in an Order.
// ═══════════════════════════════════════════════════════════

public record OrderPlaced(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string DeliveryAddress,
    List<OrderItem> Items,
    decimal TotalAmount
) : DomainEvent;

public record OrderAccepted(
    Guid OrderId,
    string RestaurantName,
    int EstimatedPrepMinutes
) : DomainEvent;

public record OrderRejected(
    Guid OrderId,
    string Reason
) : DomainEvent;

public record OrderPickedUp(
    Guid OrderId,
    string DriverName
) : DomainEvent;

public record OrderDelivered(
    Guid OrderId
) : DomainEvent;

public record OrderCancelled(
    Guid OrderId,
    string Reason
) : DomainEvent;

// ═══════════════════════════════════════════════════════════
// INTEGRATION EVENTS
// These are what gets published to SNS for other services.
// They are a DIFFERENT type from domain events — curated
// for external consumers.
// ═══════════════════════════════════════════════════════════

public record OrderPlacedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string DeliveryAddress,
    List<OrderItem> Items,
    decimal TotalAmount,
    DateTime OccurredAt
);

public record OrderCancelledIntegrationEvent(
    Guid OrderId,
    string Reason,
    DateTime OccurredAt
);

// ═══════════════════════════════════════════════════════════
// SHARED TYPES
// ═══════════════════════════════════════════════════════════

public record OrderItem(
    string ItemName,
    int Quantity,
    decimal UnitPrice
);
