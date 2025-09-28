using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating sub-orchestrator functionality with the new CallSubOrchestratorAsync API.
/// Shows how a parent orchestrator can call child orchestrators for complex workflow composition.
/// </summary>
public class SubOrchestratorExample
{
    public static async Task RunExample()
    {
        // Setup
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Register child orchestrators for different order processing steps
        runtime.RegisterOrchestrator<SubOrchestratorOrderRequest, string>("ValidateOrderOrchestrator", async (context, order) =>
        {
            Console.WriteLine($"[Child] Validating order {order.Id}");
            await context.CallAsync("CheckInventory", order.ProductId);
            await context.CallAsync("ValidateCustomer", order.CustomerId);
            return $"Order {order.Id} validated successfully";
        });

        runtime.RegisterOrchestrator<SubOrchestratorOrderRequest, string>("PaymentOrchestrator", async (context, order) =>
        {
            Console.WriteLine($"[Child] Processing payment for order {order.Id}");
            await context.CallAsync("ChargeCard", order.PaymentInfo);
            await context.CallAsync("SendPaymentConfirmation", order.CustomerId);
            return $"Payment processed for order {order.Id}";
        });

        runtime.RegisterOrchestrator<SubOrchestratorOrderRequest, string>("FulfillmentOrchestrator", async (context, order) =>
        {
            Console.WriteLine($"[Child] Fulfilling order {order.Id}");
            await context.CallAsync("ReserveInventory", order.ProductId);
            await context.CallAsync("CreateShippingLabel", order);
            await context.CallAsync("NotifyWarehouse", order.Id);
            return $"Order {order.Id} ready for shipment";
        });

        // Register the main parent orchestrator that coordinates sub-orchestrators
        runtime.RegisterOrchestrator<SubOrchestratorOrderRequest, string>("MainOrderOrchestrator", async (context, order) =>
        {
            Console.WriteLine($"[Parent] Starting order processing for {order.Id}");

            try
            {
                // Call validation sub-orchestrator
                var validationResult = await context.CallAsync<string>(
                    "ValidateOrderOrchestrator", order);
                Console.WriteLine($"[Parent] Validation completed: {validationResult}");

                // Call payment sub-orchestrator  
                var paymentResult = await context.CallAsync<string>(
                    "PaymentOrchestrator", order);
                Console.WriteLine($"[Parent] Payment completed: {paymentResult}");

                // Call fulfillment sub-orchestrator
                var fulfillmentResult = await context.CallAsync<string>(
                    "FulfillmentOrchestrator", order);
                Console.WriteLine($"[Parent] Fulfillment completed: {fulfillmentResult}");

                return $"Order {order.Id} fully processed - Validation: ✓, Payment: ✓, Fulfillment: ✓";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Parent] Order processing failed: {ex.Message}");
                await context.CallAsync("HandleOrderFailure", order.Id);
                return $"Order {order.Id} failed: {ex.Message}";
            }
        });

        // Register mock activities
        runtime.RegisterFunction<string, string>("CheckInventory", async productId =>
        {
            await Task.Delay(100);
            return $"Inventory checked for {productId}";
        });

        runtime.RegisterFunction<string, string>("ValidateCustomer", async customerId =>
        {
            await Task.Delay(50);
            return $"Customer {customerId} validated";
        });

        runtime.RegisterFunction<string, string>("ChargeCard", async paymentInfo =>
        {
            await Task.Delay(200);
            return $"Card charged: {paymentInfo}";
        });

        runtime.RegisterFunction<string, string>("SendPaymentConfirmation", async customerId =>
        {
            await Task.Delay(50);
            return $"Payment confirmation sent to {customerId}";
        });

        runtime.RegisterFunction<string, string>("ReserveInventory", async productId =>
        {
            await Task.Delay(100);
            return $"Inventory reserved for {productId}";
        });

        runtime.RegisterFunction<SubOrchestratorOrderRequest, string>("CreateShippingLabel", async order =>
        {
            await Task.Delay(150);
            return $"Shipping label created for order {order.Id}";
        });

        runtime.RegisterFunction<string, string>("NotifyWarehouse", async orderId =>
        {
            await Task.Delay(50);
            return $"Warehouse notified for order {orderId}";
        });

        runtime.RegisterFunction<string, string>("HandleOrderFailure", async orderId =>
        {
            await Task.Delay(50);
            return $"Failure handled for order {orderId}";
        });

        // Create sample order
        var sampleOrder = new SubOrchestratorOrderRequest
        {
            Id = "ORD-12345",
            CustomerId = "CUST-67890",
            ProductId = "PROD-ABCDE",
            PaymentInfo = "**** **** **** 1234"
        };

        Console.WriteLine("=== Sub-Orchestrator Example ===");
        Console.WriteLine("Demonstrating parent orchestrator calling multiple child orchestrators");
        Console.WriteLine();

        // Trigger the main orchestrator
        await runtime.TriggerAsyncObject("order-example", "MainOrderOrchestrator", sampleOrder);

        // Run the runtime
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await runtime.RunAndPollAsync(cts.Token);

        Console.WriteLine();
        Console.WriteLine("=== Sub-Orchestrator Example Completed ===");
    }
}

public class SubOrchestratorOrderRequest
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string PaymentInfo { get; set; } = "";
}