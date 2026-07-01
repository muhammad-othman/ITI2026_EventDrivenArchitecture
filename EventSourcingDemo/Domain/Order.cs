using EventSourcingDemo.Events;

namespace EventSourcingDemo.Domain;

// ═══════════════════════════════════════════════════════════
// ORDER AGGREGATE (Event-Sourced)
//
// This is the heart of Event Sourcing. The Order aggregate:
//   1. Enforces business rules in COMMAND methods
//   2. Produces events when rules pass
//   3. Updates its own state in APPLY methods
//   4. Can rebuild itself from a sequence of past events
//
// KEY RULE: Command methods decide IF something should happen.
//           Apply methods describe WHAT changes when it does.
//           Apply methods NEVER contain business logic.
// ═══════════════════════════════════════════════════════════

public class Order
{
    // --- State (rebuilt from events) ---
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; } = "";
    public string DeliveryAddress { get; private set; } = "";
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();
    public string? RestaurantName { get; private set; }
    public int? EstimatedPrepMinutes { get; private set; }
    public string? DriverName { get; private set; }
    public string? CancellationReason { get; private set; }

    // --- Event tracking ---
    private readonly List<DomainEvent> _uncommittedEvents = new();
    public IReadOnlyList<DomainEvent> UncommittedEvents => _uncommittedEvents;
    public int Version { get; private set; } = -1;

    // Private constructor — create orders only through Place()
    private Order() { }

    // ═══════════════════════════════════════════
    // COMMAND METHODS
    // These enforce business rules and raise events.
    // They NEVER set state directly.
    // ═══════════════════════════════════════════

    /// <summary>
    /// Creates a new order. This is the entry point for the aggregate.
    /// </summary>
    public static Order Place(
        Guid customerId,
        string customerName,
        string deliveryAddress,
        List<OrderItem> items)
    {
        // --- Business rules ---
        if (string.IsNullOrWhiteSpace(customerName))
            throw new InvalidOperationException("Customer name is required.");

        if (string.IsNullOrWhiteSpace(deliveryAddress))
            throw new InvalidOperationException("Delivery address is required.");

        if (items == null || items.Count == 0)
            throw new InvalidOperationException("Order must have at least one item.");

        if (items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("All items must have a positive quantity.");

        // --- Rules passed — raise the event ---
        var order = new Order();
        var total = items.Sum(i => i.Quantity * i.UnitPrice);

        order.RaiseEvent(new OrderPlaced(
            OrderId: Guid.NewGuid(),
            CustomerId: customerId,
            CustomerName: customerName,
            DeliveryAddress: deliveryAddress,
            Items: items,
            TotalAmount: total
        ));

        return order;
    }

    /// <summary>
    /// Restaurant accepts the order and provides a prep time estimate.
    /// </summary>
    public void Accept(string restaurantName, int estimatedPrepMinutes)
    {
        if (Status != OrderStatus.Placed)
            throw new InvalidOperationException(
                $"Cannot accept an order in '{Status}' status. Order must be in 'Placed' status.");

        if (string.IsNullOrWhiteSpace(restaurantName))
            throw new InvalidOperationException("Restaurant name is required.");

        if (estimatedPrepMinutes <= 0)
            throw new InvalidOperationException("Estimated prep time must be positive.");

        RaiseEvent(new OrderAccepted(Id, restaurantName, estimatedPrepMinutes));
    }

    /// <summary>
    /// Restaurant rejects the order.
    /// </summary>
    public void Reject(string reason)
    {
        if (Status != OrderStatus.Placed)
            throw new InvalidOperationException(
                $"Cannot reject an order in '{Status}' status. Order must be in 'Placed' status.");

        RaiseEvent(new OrderRejected(Id, reason));
    }

    /// <summary>
    /// Driver picks up the order from the restaurant.
    /// </summary>
    public void MarkPickedUp(string driverName)
    {
        if (Status != OrderStatus.Accepted)
            throw new InvalidOperationException(
                $"Cannot mark as picked up in '{Status}' status. Order must be 'Accepted' first.");

        if (string.IsNullOrWhiteSpace(driverName))
            throw new InvalidOperationException("Driver name is required.");

        RaiseEvent(new OrderPickedUp(Id, driverName));
    }

    /// <summary>
    /// Order has been delivered to the customer.
    /// </summary>
    public void MarkDelivered()
    {
        if (Status != OrderStatus.PickedUp)
            throw new InvalidOperationException(
                $"Cannot mark as delivered in '{Status}' status. Order must be 'PickedUp' first.");

        RaiseEvent(new OrderDelivered(Id));
    }

    /// <summary>
    /// Cancel the order. Can be cancelled in most states except Delivered.
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered order.");

        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Cancellation reason is required.");

        RaiseEvent(new OrderCancelled(Id, reason));
    }

    // ═══════════════════════════════════════════
    // APPLY METHODS
    // These update the aggregate's state.
    // They contain ZERO business logic.
    // They are called both when raising new events
    // AND when replaying events from the store.
    // ═══════════════════════════════════════════

    private void Apply(OrderPlaced e)
    {
        Id = e.OrderId;
        CustomerId = e.CustomerId;
        CustomerName = e.CustomerName;
        DeliveryAddress = e.DeliveryAddress;
        Items = e.Items;
        TotalAmount = e.TotalAmount;
        Status = OrderStatus.Placed;
    }

    private void Apply(OrderAccepted e)
    {
        Status = OrderStatus.Accepted;
        RestaurantName = e.RestaurantName;
        EstimatedPrepMinutes = e.EstimatedPrepMinutes;
    }

    private void Apply(OrderRejected e)
    {
        Status = OrderStatus.Rejected;
    }

    private void Apply(OrderPickedUp e)
    {
        Status = OrderStatus.PickedUp;
        DriverName = e.DriverName;
    }

    private void Apply(OrderDelivered e)
    {
        Status = OrderStatus.Delivered;
    }

    private void Apply(OrderCancelled e)
    {
        Status = OrderStatus.Cancelled;
        CancellationReason = e.Reason;
    }

    // ═══════════════════════════════════════════
    // INFRASTRUCTURE
    // Connects command methods to apply methods
    // and handles event replay from the store.
    // ═══════════════════════════════════════════

    /// <summary>
    /// Called by command methods. Applies the event to update state
    /// AND stores it as uncommitted for later persistence.
    /// </summary>
    private void RaiseEvent(DomainEvent @event)
    {
        ApplyEvent(@event);
        _uncommittedEvents.Add(@event);
    }

    /// <summary>
    /// Routes an event to the correct Apply method and increments the version.
    /// </summary>
    private void ApplyEvent(DomainEvent @event)
    {
        switch (@event)
        {
            case OrderPlaced e:    Apply(e); break;
            case OrderAccepted e:  Apply(e); break;
            case OrderRejected e:  Apply(e); break;
            case OrderPickedUp e:  Apply(e); break;
            case OrderDelivered e: Apply(e); break;
            case OrderCancelled e: Apply(e); break;
            default:
                throw new InvalidOperationException(
                    $"Unknown event type: {@event.GetType().Name}");
        }
        Version++;
    }

    /// <summary>
    /// Rebuilds an Order aggregate from a sequence of stored events.
    /// Used when loading an order from the Event Store.
    /// </summary>
    public static Order LoadFromHistory(IEnumerable<DomainEvent> history)
    {
        var order = new Order();
        foreach (var e in history)
        {
            order.ApplyEvent(e);
            // Note: we do NOT add to _uncommittedEvents here
            // because these events are already persisted.
        }
        return order;
    }

    /// <summary>
    /// Clears the uncommitted events after they have been saved.
    /// </summary>
    public void ClearUncommittedEvents()
    {
        _uncommittedEvents.Clear();
    }
}

public enum OrderStatus
{
    Placed,
    Accepted,
    Rejected,
    PickedUp,
    Delivered,
    Cancelled
}
