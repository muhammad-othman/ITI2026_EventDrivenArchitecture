using EventSourcingDemo.EventStore;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════
// EVENT STORE CONFIGURATION
//
// Option 1: In-Memory (default — no setup needed, great for demos)
// Option 2: SQL Server (uncomment below, run setup-database.sql first)
// ═══════════════════════════════════════════════════════════

// --- Option 1: In-Memory Event Store (default) ---
// builder.Services.AddSingleton<IEventStore, InMemoryEventStore>();

// --- Option 2: SQL Server Event Store ---
// Uncomment these lines and comment out the InMemoryEventStore line above.
// Make sure to run setup-database.sql first to create the Events table.
//
var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? "Server=(localdb)\\mssqllocaldb;Database=EventSourcingDemo;Trusted_Connection=True;";
builder.Services.AddSingleton<IEventStore>(new SqlServerEventStore(connectionString));

// ═══════════════════════════════════════════════════════════
// EVENT PUBLISHER
// Using ConsoleEventPublisher for the demo (logs to console).
// In production, you'd use SnsEventPublisher.
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<ConsoleEventPublisher>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Add this AFTER var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

Console.WriteLine("═══════════════════════════════════════════════");
Console.WriteLine("  EVENT SOURCING DEMO — Order Service");
Console.WriteLine("  Using: In-Memory Event Store");
Console.WriteLine("  API: http://localhost:5000");
Console.WriteLine("═══════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  Endpoints:");
Console.WriteLine("    POST   /api/orders                  Place an order");
Console.WriteLine("    POST   /api/orders/{id}/accept      Accept an order");
Console.WriteLine("    POST   /api/orders/{id}/reject      Reject an order");
Console.WriteLine("    POST   /api/orders/{id}/pickup      Mark picked up");
Console.WriteLine("    POST   /api/orders/{id}/deliver     Mark delivered");
Console.WriteLine("    POST   /api/orders/{id}/cancel      Cancel an order");
Console.WriteLine("    GET    /api/orders/{id}             Get current state");
Console.WriteLine("    GET    /api/orders/{id}/history     Get event history");
Console.WriteLine();

app.Run("http://localhost:5000");
