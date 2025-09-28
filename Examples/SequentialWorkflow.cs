using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

public class OrderRequest
{
    public string Id { get; set; } = "";
    public decimal Amount { get; set; }
    public string CustomerEmail { get; set; } = "";
}

/// <summary>
/// Sequential workflow example showing step-by-step order processing
/// </summary>
public class SequentialWorkflowExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Register the orchestrator
        runtime.RegisterOrchestratorFunction<OrderRequest, string>("ProcessOrderOrchestrator", async context =>
        {
            var order = context.GetInput<OrderRequest>();
            
            // Sequential processing - each step waits for the previous
            var validated = await context.CallFunction<OrderRequest>("ValidateOrder", order);
            var charged = await context.CallFunction<OrderRequest>("ChargePayment", validated);  
            var shipped = await context.CallFunction<OrderRequest>("ShipOrder", charged);
            var notified = await context.CallFunction<string>("NotifyCustomer", shipped);
            
            return $"Order {order.Id} processed successfully! {notified}";
        });

        // Register the activity functions
        runtime.RegisterFunction<OrderRequest, OrderRequest>("ValidateOrder", async order =>
        {
            Console.WriteLine($"Validating order {order.Id}...");
            await Task.Delay(500); // Simulate validation
            if (order.Amount <= 0) throw new ArgumentException("Invalid amount");
            return order;
        });

        runtime.RegisterFunction<OrderRequest, OrderRequest>("ChargePayment", async order =>
        {
            Console.WriteLine($"Charging ${order.Amount} for order {order.Id}...");
            await Task.Delay(1000); // Simulate payment processing
            return order;
        });

        runtime.RegisterFunction<OrderRequest, OrderRequest>("ShipOrder", async order =>
        {
            Console.WriteLine($"Shipping order {order.Id}...");
            await Task.Delay(800); // Simulate shipping
            return order;
        });

        runtime.RegisterFunction<OrderRequest, string>("NotifyCustomer", async order =>
        {
            Console.WriteLine($"Notifying customer {order.CustomerEmail} about order {order.Id}...");
            await Task.Delay(300); // Simulate notification
            return "Customer notified";
        });

        // Run the example
        var sampleOrder = new OrderRequest
        {
            Id = "ORD-001",
            Amount = 99.99m,
            CustomerEmail = "customer@example.com"
        };

        await runtime.TriggerAsync("order-001", "ProcessOrderOrchestrator", sampleOrder);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await runtime.RunAndPollAsync(cts.Token);
    }
}