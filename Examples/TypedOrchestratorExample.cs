using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating the new typed orchestrator functionality.
/// Shows how to use RegisterOrchestratorFunction<TIn, TOut> with GetInput<T>() and GetLogger().
/// </summary>
public static class TypedOrchestratorExample
{
    public class OrderRequest
    {
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class OrderResult
    {
        public string OrderId { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "";
        public DateTime ProcessedAt { get; set; }
    }

    public static async Task RunExample()
    {
        Console.WriteLine("=== Typed Orchestrator Example ===\n");

        // Setup the runtime
        var stateStore = new InMemoryStateStore();
        using var loggerFactory =
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Register activities
        runtime.RegisterFunction<OrderRequest, string>("ProcessPayment", async (order) =>
        {
            Console.WriteLine($"💳 Processing payment for {order.ProductName} - ${order.Price * order.Quantity}");
            await Task.Delay(500); // Simulate payment processing
            return $"PAYMENT_{Guid.NewGuid():N}";
        });

        runtime.RegisterFunction<string, string>("SendConfirmationEmail", async (orderId) =>
        {
            Console.WriteLine($"📧 Sending confirmation email for order {orderId}");
            await Task.Delay(300); // Simulate email sending
            return "EMAIL_SENT";
        });

        // Register typed orchestrator using the new RegisterOrchestratorFunction<TIn, TOut> method
        runtime.RegisterOrchestrator<OrderRequest, OrderResult>("ProcessOrder",
            async (context, orderRequest) =>
            {
                // Use GetLogger() to get a logger instance
                var orchestratorLogger = context.GetLogger();
                orchestratorLogger.LogInformation("🎯 Starting order processing for {ProductName}",
                    orderRequest.ProductName);

                try
                {
                    // Process payment
                    var paymentId = await context.CallAsync<string>("ProcessPayment", orderRequest);
                    orchestratorLogger.LogInformation("✅ Payment processed: {PaymentId}", paymentId);

                    // Generate order ID
                    var orderId = $"ORDER_{Guid.NewGuid():N[..8].ToUpper()}";

                    // Send confirmation email
                    var emailResult = await context.CallAsync<string>("SendConfirmationEmail", orderId);
                    orchestratorLogger.LogInformation("✅ Email sent: {EmailResult}", emailResult);

                    // Return typed result
                    return new OrderResult
                    {
                        OrderId = orderId,
                        TotalAmount = orderRequest.Price * orderRequest.Quantity,
                        Status = "COMPLETED",
                        ProcessedAt = DateTime.UtcNow
                    };
                }
                catch (Exception ex)
                {
                    orchestratorLogger.LogError(ex, "❌ Error processing order");
                    return new OrderResult
                    {
                        OrderId = "ERROR",
                        Status = "FAILED",
                        ProcessedAt = DateTime.UtcNow
                    };
                }
            });

        // Create a sample order
        var sampleOrder = new OrderRequest
        {
            ProductName = "Laptop",
            Quantity = 2,
            Price = 999.99m
        };

        // Trigger the orchestration with typed input
        Console.WriteLine("🚀 Triggering ProcessOrder orchestration...\n");
        await runtime.TriggerAsyncObject("abc", "ProcessOrder", sampleOrder);

        // Run the runtime to execute the workflow
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n✅ Example completed!");
        }
    }
}