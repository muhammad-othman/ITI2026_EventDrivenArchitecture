# ═══════════════════════════════════════════════════════════
# Test the full order lifecycle through Event Sourcing
# ═══════════════════════════════════════════════════════════
# Run this after starting the API with: dotnet run

$baseUrl = "http://localhost:5000/api/orders"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Event Sourcing Demo — Order Lifecycle Test" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# --- Step 1: Place Order ---
Write-Host ""
Write-Host "STEP 1: Placing order..." -ForegroundColor Yellow

$placeBody = @"
{
    "customerId": "550e8400-e29b-41d4-a716-446655440000",
    "customerName": "Mahmoud Ibrahim",
    "deliveryAddress": "42 Tahrir Square, Cairo",
    "items": [
        { "itemName": "Margherita Pizza", "quantity": 2, "unitPrice": 89.99 },
        { "itemName": "Garlic Bread", "quantity": 1, "unitPrice": 35.00 }
    ]
}
"@

$orderResponse = Invoke-RestMethod -Uri $baseUrl -Method Post -Body $placeBody -ContentType "application/json"
$orderId = $orderResponse.orderId
Write-Host "  Order placed! ID: $orderId" -ForegroundColor Green
Write-Host "  Status: $($orderResponse.status), Version: $($orderResponse.version)" -ForegroundColor White
Write-Host ""

Read-Host "Press Enter for Step 2 (Accept)"

# --- Step 2: Accept Order ---
Write-Host "STEP 2: Restaurant accepting order..." -ForegroundColor Yellow

$acceptBody = '{ "restaurantName": "Pizza Palace", "estimatedPrepMinutes": 25 }'
$acceptResponse = Invoke-RestMethod -Uri "$baseUrl/$orderId/accept" -Method Post -Body $acceptBody -ContentType "application/json"
Write-Host "  Status: $($acceptResponse.status), Version: $($acceptResponse.version)" -ForegroundColor Green
Write-Host ""

Read-Host "Press Enter for Step 3 (Pickup)"

# --- Step 3: Mark Picked Up ---
Write-Host "STEP 3: Driver picking up order..." -ForegroundColor Yellow

$pickupBody = '{ "driverName": "Ahmed Hassan" }'
$pickupResponse = Invoke-RestMethod -Uri "$baseUrl/$orderId/pickup" -Method Post -Body $pickupBody -ContentType "application/json"
Write-Host "  Status: $($pickupResponse.status), Driver: $($pickupResponse.driverName), Version: $($pickupResponse.version)" -ForegroundColor Green
Write-Host ""

Read-Host "Press Enter for Step 4 (Deliver)"

# --- Step 4: Mark Delivered ---
Write-Host "STEP 4: Marking order as delivered..." -ForegroundColor Yellow

$deliverResponse = Invoke-RestMethod -Uri "$baseUrl/$orderId/deliver" -Method Post -ContentType "application/json"
Write-Host "  Status: $($deliverResponse.status), Version: $($deliverResponse.version)" -ForegroundColor Green
Write-Host ""

Read-Host "Press Enter to view the Event History"

# --- Step 5: View Event History ---
Write-Host "STEP 5: Full event history for order $orderId" -ForegroundColor Yellow
Write-Host ""

$history = Invoke-RestMethod -Uri "$baseUrl/$orderId/history" -Method Get
Write-Host "  Total events: $($history.eventCount)" -ForegroundColor Cyan
Write-Host ""

foreach ($event in $history.history) {
    Write-Host "  [$($event.version)] $($event.eventType) at $($event.occurredAt)" -ForegroundColor White
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Key Observations:" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  1. The Event Store has $($history.eventCount) events for this order." -ForegroundColor White
Write-Host "     In a traditional DB, you'd have 1 row showing 'Delivered'." -ForegroundColor White
Write-Host "     With Event Sourcing, you have the COMPLETE history." -ForegroundColor White
Write-Host ""
Write-Host "  2. The current state (Delivered) was COMPUTED by replaying" -ForegroundColor White
Write-Host "     all events. The state is not stored — it is derived." -ForegroundColor White
Write-Host ""
Write-Host "  3. You can answer questions like:" -ForegroundColor White
Write-Host "     - When was the order accepted? (check event timestamps)" -ForegroundColor White
Write-Host "     - Who was the driver? (from OrderPickedUp event)" -ForegroundColor White
Write-Host "     - How long from placed to delivered? (first - last timestamp)" -ForegroundColor White
Write-Host ""

Read-Host "Press Enter to test business rule enforcement"

# --- Step 6: Test Business Rules ---
Write-Host ""
Write-Host "STEP 6: Trying to cancel a delivered order..." -ForegroundColor Yellow
Write-Host "  (This should FAIL - business rule violation)" -ForegroundColor Yellow
Write-Host ""

try {
    $cancelBody = '{ "reason": "Changed my mind" }'
    Invoke-RestMethod -Uri "$baseUrl/$orderId/cancel" -Method Post -Body $cancelBody -ContentType "application/json"
    Write-Host "  Unexpected: cancel succeeded!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    Write-Host "  Correctly rejected: $($errorResponse.error)" -ForegroundColor Green
}

Write-Host ""
Write-Host "  The aggregate enforced: 'Cannot cancel a delivered order.'" -ForegroundColor White
Write-Host "  The event was never created because the business rule failed." -ForegroundColor White
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Test complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
