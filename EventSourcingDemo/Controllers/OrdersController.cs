using EventSourcingDemo.Domain;
using EventSourcingDemo.Events;
using EventSourcingDemo.EventStore;
using Microsoft.AspNetCore.Mvc;

namespace EventSourcingDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IEventStore _eventStore;
    private readonly ConsoleEventPublisher _publisher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IEventStore eventStore,
        ConsoleEventPublisher publisher,
        ILogger<OrdersController> logger)
    {
        _eventStore = eventStore;
        _publisher = publisher;
        _logger = logger;
    }

    // ═══════════════════════════════════════════
    // PLACE ORDER
    // Creates a new Order aggregate and saves the OrderPlaced event.
    // ═══════════════════════════════════════════

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        try
        {
            // 1. Create the aggregate (validates rules, raises domain events)
            var order = Order.Place(
                request.CustomerId,
                request.CustomerName,
                request.DeliveryAddress,
                request.Items);

            // 2. Save domain events to the Event Store
            //    expectedVersion = -1 because this is a new aggregate
            await _eventStore.SaveEventsAsync(
                order.Id,
                order.UncommittedEvents,
                expectedVersion: -1);

            _logger.LogInformation(
                "[EventStore] Saved {Count} event(s) for new order {OrderId}",
                order.UncommittedEvents.Count, order.Id);

            // 3. Publish integration event to SNS (for other services)
            await _publisher.PublishAsync(new OrderPlacedIntegrationEvent(
                order.Id, request.CustomerId, request.CustomerName,
                request.DeliveryAddress, request.Items,
                order.TotalAmount, DateTime.UtcNow));

            return Accepted(new
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                Version = order.Version
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════
    // ACCEPT ORDER
    // Loads the aggregate from history, applies the Accept command,
    // and saves the new event.
    // ═══════════════════════════════════════════

    [HttpPost("{orderId}/accept")]
    public async Task<IActionResult> AcceptOrder(
        Guid orderId, [FromBody] AcceptOrderRequest request)
    {
        try
        {
            // 1. Load all events for this order from the Event Store
            var events = await _eventStore.GetEventsAsync(orderId);
            if (events.Count == 0)
                return NotFound(new { Error = $"Order {orderId} not found." });

            // 2. Rebuild the aggregate from its event history
            var order = Order.LoadFromHistory(events);

            _logger.LogInformation(
                "[EventStore] Loaded order {OrderId} at version {Version}, status: {Status}",
                order.Id, order.Version, order.Status);

            // 3. Execute the command (validates business rules, raises events)
            order.Accept(request.RestaurantName, request.EstimatedPrepMinutes);

            // 4. Save the NEW events
            //    expectedVersion = version BEFORE the new events were raised
            var versionBeforeCommand = order.Version - order.UncommittedEvents.Count;
            await _eventStore.SaveEventsAsync(
                order.Id,
                order.UncommittedEvents,
                versionBeforeCommand);

            _logger.LogInformation(
                "[EventStore] Saved {Count} new event(s) for order {OrderId}, now at version {Version}",
                order.UncommittedEvents.Count, order.Id, order.Version);

            return Ok(new
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                RestaurantName = order.RestaurantName,
                Version = order.Version
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (ConcurrencyException ex)
        {
            return Conflict(new { Error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════
    // REJECT ORDER
    // ═══════════════════════════════════════════

    [HttpPost("{orderId}/reject")]
    public async Task<IActionResult> RejectOrder(
        Guid orderId, [FromBody] RejectOrderRequest request)
    {
        try
        {
            var events = await _eventStore.GetEventsAsync(orderId);
            if (events.Count == 0)
                return NotFound(new { Error = $"Order {orderId} not found." });

            var order = Order.LoadFromHistory(events);
            order.Reject(request.Reason);

            var versionBeforeCommand = order.Version - order.UncommittedEvents.Count;
            await _eventStore.SaveEventsAsync(order.Id, order.UncommittedEvents, versionBeforeCommand);

            _logger.LogInformation("[EventStore] Order {OrderId} rejected", order.Id);

            return Ok(new
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                Version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (ConcurrencyException ex) { return Conflict(new { Error = ex.Message }); }
    }

    // ═══════════════════════════════════════════
    // MARK PICKED UP
    // ═══════════════════════════════════════════

    [HttpPost("{orderId}/pickup")]
    public async Task<IActionResult> MarkPickedUp(
        Guid orderId, [FromBody] PickupRequest request)
    {
        try
        {
            var events = await _eventStore.GetEventsAsync(orderId);
            if (events.Count == 0)
                return NotFound(new { Error = $"Order {orderId} not found." });

            var order = Order.LoadFromHistory(events);
            order.MarkPickedUp(request.DriverName);

            var versionBeforeCommand = order.Version - order.UncommittedEvents.Count;
            await _eventStore.SaveEventsAsync(order.Id, order.UncommittedEvents, versionBeforeCommand);

            _logger.LogInformation("[EventStore] Order {OrderId} picked up by {Driver}", order.Id, request.DriverName);

            return Ok(new
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                DriverName = order.DriverName,
                Version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (ConcurrencyException ex) { return Conflict(new { Error = ex.Message }); }
    }

    // ═══════════════════════════════════════════
    // MARK DELIVERED
    // ═══════════════════════════════════════════

    [HttpPost("{orderId}/deliver")]
    public async Task<IActionResult> MarkDelivered(Guid orderId)
    {
        try
        {
            var events = await _eventStore.GetEventsAsync(orderId);
            if (events.Count == 0)
                return NotFound(new { Error = $"Order {orderId} not found." });

            var order = Order.LoadFromHistory(events);
            order.MarkDelivered();

            var versionBeforeCommand = order.Version - order.UncommittedEvents.Count;
            await _eventStore.SaveEventsAsync(order.Id, order.UncommittedEvents, versionBeforeCommand);

            _logger.LogInformation("[EventStore] Order {OrderId} delivered", order.Id);

            return Ok(new
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                Version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (ConcurrencyException ex) { return Conflict(new { Error = ex.Message }); }
    }

    // ═══════════════════════════════════════════
    // CANCEL ORDER
    // ═══════════════════════════════════════════

    [HttpPost("{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(
        Guid orderId, [FromBody] CancelOrderRequest request)
    {
        try
        {
            var events = await _eventStore.GetEventsAsync(orderId);
            if (events.Count == 0)
                return NotFound(new { Error = $"Order {orderId} not found." });

            var order = Order.LoadFromHistory(events);
            order.Cancel(request.Reason);

            var versionBeforeCommand = order.Version - order.UncommittedEvents.Count;
            await _eventStore.SaveEventsAsync(order.Id, order.UncommittedEvents, versionBeforeCommand);

            _logger.LogInformation("[EventStore] Order {OrderId} cancelled: {Reason}", order.Id, request.Reason);

            await _publisher.PublishAsync(new OrderCancelledIntegrationEvent(
                order.Id, request.Reason, DateTime.UtcNow));

            return Ok(new
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                Reason = request.Reason,
                Version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (ConcurrencyException ex) { return Conflict(new { Error = ex.Message }); }
    }

    // ═══════════════════════════════════════════
    // GET ORDER (Replay events to show current state)
    // This is the "naive" query approach — we'll replace it
    // with CQRS projections in Session 6.
    // ═══════════════════════════════════════════

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId)
    {
        var events = await _eventStore.GetEventsAsync(orderId);
        if (events.Count == 0)
            return NotFound(new { Error = $"Order {orderId} not found." });

        var order = Order.LoadFromHistory(events);

        return Ok(new
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            DeliveryAddress = order.DeliveryAddress,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            Items = order.Items,
            RestaurantName = order.RestaurantName,
            EstimatedPrepMinutes = order.EstimatedPrepMinutes,
            DriverName = order.DriverName,
            CancellationReason = order.CancellationReason,
            Version = order.Version,
            EventCount = events.Count
        });
    }

    // ═══════════════════════════════════════════
    // GET EVENT HISTORY (Show the raw events for an order)
    // This is a unique advantage of Event Sourcing —
    // you can see exactly what happened and when.
    // ═══════════════════════════════════════════

    [HttpGet("{orderId}/history")]
    public async Task<IActionResult> GetOrderHistory(Guid orderId)
    {
        var events = await _eventStore.GetEventsAsync(orderId);
        if (events.Count == 0)
            return NotFound(new { Error = $"Order {orderId} not found." });

        var history = events.Select((e, index) => new
        {
            Version = index,
            EventType = e.GetType().Name,
            OccurredAt = e.OccurredAt,
            Data = e
        });

        return Ok(new
        {
            OrderId = orderId,
            EventCount = events.Count,
            History = history
        });
    }
}

// ═══════════════════════════════════════════
// REQUEST MODELS
// ═══════════════════════════════════════════

public record PlaceOrderRequest(
    Guid CustomerId,
    string CustomerName,
    string DeliveryAddress,
    List<OrderItem> Items
);

public record AcceptOrderRequest(
    string RestaurantName,
    int EstimatedPrepMinutes
);

public record RejectOrderRequest(string Reason);
public record PickupRequest(string DriverName);
public record CancelOrderRequest(string Reason);
