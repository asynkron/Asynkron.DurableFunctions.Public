using System.Diagnostics;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Models;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating OpenTelemetry distributed tracing with Asynkron.DurableFunctions.
/// This example shows how activities are automatically created for orchestrations, function calls, and state operations.
/// </summary>
public class OpenTelemetryExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("🔍 OpenTelemetry Tracing Example");
        Console.WriteLine("=================================");
        Console.WriteLine();

        // Setup logging
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Capture activities for demonstration
        var capturedActivities = new List<Activity>();
        ActivitySource.AddActivityListener(new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Asynkron.DurableFunctions",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                capturedActivities.Add(activity);
                Console.WriteLine($"🚀 Started: {activity.OperationName} - {activity.GetTagItem("function.name")}");
            },
            ActivityStopped = activity =>
            {
                Console.WriteLine($"✅ Stopped: {activity.OperationName} - Duration: {activity.Duration.TotalMilliseconds:F1}ms");
            }
        });

        // Register order processing orchestrator
        runtime.RegisterOrchestrator<TraceOrderRequest, TraceOrderResult>("ProcessOrderOrchestrator", async (context, order) =>
        {
            Console.WriteLine($"[Orchestrator] Processing order: {order.OrderId}");

            // Step 1: Validate payment
            var paymentResult = await context.CallAsync<PaymentValidation>("ValidatePayment", order.Payment);
            
            if (!paymentResult.IsValid)
            {
                return new TraceOrderResult { OrderId = order.OrderId, Status = "PaymentFailed", Message = paymentResult.FailureReason };
            }

            // Step 2: Reserve inventory
            var inventoryResult = await context.CallAsync<InventoryReservation>("ReserveInventory", order.Items);
            
            if (!inventoryResult.Success)
            {
                return new TraceOrderResult { OrderId = order.OrderId, Status = "InventoryUnavailable", Message = inventoryResult.Message };
            }

            // Step 3: Create shipment
            var shipmentResult = await context.CallAsync<ShipmentCreation>("CreateShipment", 
                new ShipmentRequest { OrderId = order.OrderId, Items = order.Items, Address = order.ShippingAddress });

            return new TraceOrderResult 
            { 
                OrderId = order.OrderId, 
                Status = "Completed", 
                TrackingNumber = shipmentResult.TrackingNumber,
                Message = "Order processed successfully" 
            };
        });

        // Register activity functions
        runtime.RegisterFunction<TracePaymentInfo, PaymentValidation>("ValidatePayment", payment =>
        {
            Console.WriteLine($"[Activity] Validating payment: {payment.CardNumber[..4]}****");
            
            // Simulate payment validation
            var isValid = payment.Amount > 0 && !string.IsNullOrEmpty(payment.CardNumber);
            return Task.FromResult(new PaymentValidation 
            { 
                IsValid = isValid, 
                FailureReason = isValid ? null : "Invalid payment information" 
            });
        });

        runtime.RegisterFunction<List<TraceOrderItem>, InventoryReservation>("ReserveInventory", items =>
        {
            Console.WriteLine($"[Activity] Reserving inventory for {items.Count} items");
            
            // Simulate inventory check
            var success = items.All(item => item.Quantity <= 10); // Simple rule: max 10 per item
            return Task.FromResult(new InventoryReservation 
            { 
                Success = success, 
                Message = success ? "Inventory reserved" : "Insufficient inventory" 
            });
        });

        runtime.RegisterFunction<ShipmentRequest, ShipmentCreation>("CreateShipment", request =>
        {
            Console.WriteLine($"[Activity] Creating shipment for order: {request.OrderId}");
            
            // Simulate shipment creation
            var trackingNumber = $"TRK{DateTime.Now.Ticks % 1000000:D6}";
            return Task.FromResult(new ShipmentCreation 
            { 
                TrackingNumber = trackingNumber, 
                EstimatedDelivery = DateTime.Now.AddDays(3) 
            });
        });

        // Create sample order
        var sampleOrder = new TraceOrderRequest
        {
            OrderId = "ORD-2024-001",
            Items = new List<TraceOrderItem>
            {
                new() { ProductId = "LAPTOP-001", Quantity = 1, Price = 999.99m },
                new() { ProductId = "MOUSE-001", Quantity = 2, Price = 29.99m }
            },
            Payment = new TracePaymentInfo
            {
                CardNumber = "4532123456789012",
                Amount = 1059.97m,
                Currency = "USD"
            },
            ShippingAddress = "123 Main St, Anytown, USA"
        };

        Console.WriteLine($"📦 Starting order processing workflow for: {sampleOrder.OrderId}");
        Console.WriteLine();

        // Trigger the orchestration
        await runtime.TriggerAsync("", "ProcessOrderOrchestrator", JsonSerializer.Serialize(sampleOrder));

        // Run the orchestration
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var pollingTask = runtime.RunAndPollAsync(cancellationTokenSource.Token);

        // Wait a bit for processing
        await Task.Delay(5000);
        cancellationTokenSource.Cancel();

        try
        {
            await pollingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        Console.WriteLine();
        Console.WriteLine("📊 Tracing Summary");
        Console.WriteLine("==================");
        Console.WriteLine($"Total activities captured: {capturedActivities.Count}");
        
        var groupedActivities = capturedActivities
            .GroupBy(a => a.OperationName)
            .OrderBy(g => g.Key);

        foreach (var group in groupedActivities)
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} activities");
            var avgDuration = group.Average(a => a.Duration.TotalMilliseconds);
            Console.WriteLine($"    Average duration: {avgDuration:F1}ms");
        }

        Console.WriteLine();
        Console.WriteLine("💡 In a real application, these traces would be:");
        Console.WriteLine("   • Exported to Jaeger, Zipkin, or Application Insights");
        Console.WriteLine("   • Correlated across service boundaries");
        Console.WriteLine("   • Used for performance monitoring and debugging");
        Console.WriteLine("   • Sampled to control overhead in high-throughput scenarios");
    }
}

// Data models for the example
public class TraceOrderRequest
{
    public string OrderId { get; set; } = "";
    public List<TraceOrderItem> Items { get; set; } = new();
    public TracePaymentInfo Payment { get; set; } = new();
    public string ShippingAddress { get; set; } = "";
}

public class TraceOrderItem
{
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class TracePaymentInfo
{
    public string CardNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
}

public class TraceOrderResult
{
    public string OrderId { get; set; } = "";
    public string Status { get; set; } = "";
    public string? TrackingNumber { get; set; }
    public string? Message { get; set; }
}

public class PaymentValidation
{
    public bool IsValid { get; set; }
    public string? FailureReason { get; set; }
}

public class InventoryReservation
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class ShipmentRequest
{
    public string OrderId { get; set; } = "";
    public List<TraceOrderItem> Items { get; set; } = new();
    public string Address { get; set; } = "";
}

public class ShipmentCreation
{
    public string TrackingNumber { get; set; } = "";
    public DateTime EstimatedDelivery { get; set; }
}