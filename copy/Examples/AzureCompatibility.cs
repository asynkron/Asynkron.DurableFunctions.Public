using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

// Mock Azure compatibility attributes for demonstration
public class FunctionNameAttribute : Attribute
{
    public string Name { get; }
    public FunctionNameAttribute(string name) => Name = name;
}

public class DurableOrchestrationTriggerAttribute : Attribute { }
public class DurableActivityTriggerAttribute : Attribute { }

// Mock Azure interfaces for demonstration
public interface IDurableOrchestrationContext
{
    T GetInput<T>();
    Task<T> CallActivityAsync<T>(string functionName, object input);
    Task<T> WaitForExternalEvent<T>(string eventName);
    Task CreateTimer(DateTime dueTime);
}

public interface IDurableActivityContext
{
    T GetInput<T>();
}

/// <summary>
/// Azure Durable Functions compatibility example showing drop-in replacement
/// </summary>
public class AzureCompatibilityExample
{
    // Your EXISTING Azure code - works as-is with minimal changes!
    public class OrderProcessingFunctions
    {
        [FunctionName("ProcessOrderOrchestrator")]
        public async Task<string> ProcessOrder([DurableOrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var order = context.GetInput<OrderRequest>();
            
            // These calls work exactly the same with our compatibility layer!
            var validated = await context.CallActivityAsync<OrderRequest>("ValidateOrder", order);
            var charged = await context.CallActivityAsync<OrderRequest>("ChargePayment", validated);
            var shipped = await context.CallActivityAsync<OrderRequest>("ShipOrder", charged);
            
            return $"Order {order.Id} processed successfully!";
        }

        [FunctionName("ValidateOrder")]
        public async Task<OrderRequest> ValidateOrder([DurableActivityTrigger] IDurableActivityContext context)
        {
            var order = context.GetInput<OrderRequest>();
            Console.WriteLine($"Validating order {order.Id}...");
            await Task.Delay(100);
            // Your validation logic here
            return order;
        }

        [FunctionName("ChargePayment")]
        public async Task<OrderRequest> ChargePayment([DurableActivityTrigger] IDurableActivityContext context)
        {
            var order = context.GetInput<OrderRequest>();
            Console.WriteLine($"Charging payment for order {order.Id}...");
            await Task.Delay(200);
            // Your payment logic here
            return order;
        }

        [FunctionName("ShipOrder")]
        public async Task<OrderRequest> ShipOrder([DurableActivityTrigger] IDurableActivityContext context)
        {
            var order = context.GetInput<OrderRequest>();
            Console.WriteLine($"Shipping order {order.Id}...");
            await Task.Delay(150);
            // Your shipping logic here
            return order;
        }
    }

    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Manual registration example (you would normally use the Azure adapter)
        var functions = new OrderProcessingFunctions();

        runtime.RegisterOrchestratorFunction<OrderRequest, string>("ProcessOrderOrchestrator", async context =>
        {
            // Adapter would map the context automatically
            var mockContext = new MockDurableOrchestrationContext(context);
            return await functions.ProcessOrder(mockContext);
        });

        runtime.RegisterFunction<OrderRequest, OrderRequest>("ValidateOrder", async order =>
        {
            var mockContext = new MockDurableActivityContext(order);
            return await functions.ValidateOrder(mockContext);
        });

        runtime.RegisterFunction<OrderRequest, OrderRequest>("ChargePayment", async order =>
        {
            var mockContext = new MockDurableActivityContext(order);
            return await functions.ChargePayment(mockContext);
        });

        runtime.RegisterFunction<OrderRequest, OrderRequest>("ShipOrder", async order =>
        {
            var mockContext = new MockDurableActivityContext(order);
            return await functions.ShipOrder(mockContext);
        });

        // Run the example
        var sampleOrder = new OrderRequest
        {
            Id = "AZURE-001",
            Amount = 129.99m,
            CustomerEmail = "customer@example.com"
        };

        Console.WriteLine("Running Azure compatibility example...");
        await runtime.TriggerAsync("azure-order-001", "ProcessOrderOrchestrator", sampleOrder);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await runtime.RunAndPollAsync(cts.Token);
    }

    // Mock implementations for demonstration
    private class MockDurableOrchestrationContext : IDurableOrchestrationContext
    {
        private readonly object _context;

        public MockDurableOrchestrationContext(object context)
        {
            _context = context;
        }

        public T GetInput<T>()
        {
            // This would be implemented by the real Azure adapter
            var method = _context.GetType().GetMethod("GetInput");
            return (T)method?.MakeGenericMethod(typeof(T)).Invoke(_context, null);
        }

        public async Task<T> CallActivityAsync<T>(string functionName, object input)
        {
            // This would be implemented by the real Azure adapter
            var method = _context.GetType().GetMethod("CallFunction");
            var task = (Task<T>)method?.MakeGenericMethod(typeof(T)).Invoke(_context, new[] { functionName, input });
            return await task;
        }

        public Task<T> WaitForExternalEvent<T>(string eventName)
        {
            // This would be implemented by the real Azure adapter
            var method = _context.GetType().GetMethod("WaitForExternalEvent");
            return (Task<T>)method?.MakeGenericMethod(typeof(T)).Invoke(_context, new object[] { eventName });
        }

        public Task CreateTimer(DateTime dueTime)
        {
            // This would be implemented by the real Azure adapter
            var method = _context.GetType().GetMethod("CreateTimer");
            return (Task)method?.Invoke(_context, new object[] { dueTime });
        }
    }

    private class MockDurableActivityContext : IDurableActivityContext
    {
        private readonly object _input;

        public MockDurableActivityContext(object input)
        {
            _input = input;
        }

        public T GetInput<T>()
        {
            return (T)_input;
        }
    }
}