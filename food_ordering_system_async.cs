

public async Task<OrderResult> PlaceOrder(PlaceOrderRequest request)
{
    // Save Order 
    var order = new Order(request);
    await _db.Orders.SaveChange(order);
    await _db.SaveCangesAsync();


    await _eventBus.Publish(new OrderPlaced(order));

    return OrderResult.Accepted(order);

}


/*
OrderService 300ms
===========================
300ms
*/



// Event Examples
public record OrderPlaced();
public record OrderDelivered();
public record PaymentRecevied();

// Not Events

public record PlaceOrder();
public record SendNotification();




// Past Tense
// Immutable
// Producer doesn't (or care) who is listening





public record OrderPlacedEvent(Guid OrderId, DateTime CreatedAt);



// Resaurant Service 
public async Task Handle(OrderPlacedEvent @event)
{
    var orderDetails = await _orderServiceClient.GetOrder(@event.OrderId);

    await PrepareORder(orderDetails);
}




// Event Carried Transfer State (Fat Event)



public record OrderPlacedEvent(
    Guid OrderId, 
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    string CustomerAddress,
    List<OrderItem> Items,
    // decimal Subtotal,
    // decimal Tax,
    // decimal PromoCode,
    // decimal Discount,
    decimal Total,
    string PaymentMethod,
    DateTime CreatedAt
    );