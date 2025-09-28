using Asynkron.DurableFunctions.Attributes;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example showing how to use auto-registration of functions via reflection.
/// This demonstrates the Azure Functions pattern with [Function] attributes.
/// </summary>
public static class AutoRegistrationExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Auto-Registration Example ===\n");
        Console.WriteLine("This example demonstrates auto-registration using the CORE API attributes:");
        Console.WriteLine("  - [Function] - marks functions for auto-discovery");
        Console.WriteLine("  - [OrchestrationTrigger] - marks orchestrator parameters");
        Console.WriteLine("  - [FunctionTrigger] - marks activity function parameters");
        Console.WriteLine("  - Uses context.CallAsync<T>() method calls");
        Console.WriteLine();

        // Setup the runtime
        var stateStore = new InMemoryStateStore();
        using var loggerFactory =
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Auto-register all functions from this assembly using reflection
        runtime.ScanAndRegister(typeof(AutoRegistrationExample).Assembly);

        // Trigger the orchestration
        Console.WriteLine("🚀 Triggering auto-registered orchestration...\n");
        await runtime.TriggerAsync("abc", "CreateEcommerceOrderOrchestration");

        // Run the runtime to execute the workflow
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n✅ Example completed successfully!");
        }
    }
}

/// <summary>
/// Example functions that will be auto-registered via reflection.
/// These use the core Asynkron.DurableFunctions attributes (not Azure Adapter attributes).
/// Core attributes: [Function], [OrchestrationTrigger], [FunctionTrigger]
/// </summary>
public class EcommerceOrchestrationFunctions
{
    /// <summary>
    /// Orchestrator function that processes an e-commerce order workflow.
    /// This demonstrates the core API pattern with [Function] and [OrchestrationTrigger] attributes.
    /// </summary>
    [Function("CreateEcommerceOrderOrchestration")]
    public async Task<string> EcommerceOrderOrchestration([OrchestrationTrigger] IOrchestrationContext context)
    {
        Console.WriteLine("🎯 Starting E-commerce Order Orchestration...");

        try
        {
            // Call the first activity to get input
            var input = await context.CallAsync<OrderProcessingRequest>("GetCreateEcommerceOrderOrchestrationInput",
                new OrderProcessingRequest { RequestId = "REQ-001", Type = "Order" });

            Console.WriteLine($"✅ Got orchestration input: {input.RequestId}");

            // Process the order
            var result = await context.CallAsync<string>("ProcessEcommerceOrder", input);


            Console.WriteLine($"🎉 E-commerce Order processed successfully: {result}");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error in orchestration: {ex.Message}");
            return $"Failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Activity function that gets the orchestration input.
    /// This demonstrates the core API pattern with [Function] and [FunctionTrigger] attributes.
    /// </summary>
    [Function("GetCreateEcommerceOrderOrchestrationInput")]
    public async Task<OrderProcessingRequest> GetCreateEcommerceOrderOrchestrationInput(
        [FunctionTrigger] OrderProcessingRequest request,
        FunctionContext executionContext)
    {
        Console.WriteLine($"🔧 Getting orchestration input for request: {request.RequestId}");

        // Simulate some processing
        await Task.Delay(100);

        return new OrderProcessingRequest
        {
            RequestId = request.RequestId + "-PROCESSED",
            Type = request.Type,
            Data = "Enhanced order data"
        };
    }

    /// <summary>
    /// Activity function that processes an e-commerce order.
    /// This demonstrates the core API pattern with [Function] and [FunctionTrigger] attributes.
    /// </summary>
    [Function("ProcessEcommerceOrder")]
    public async Task<string> ProcessEcommerceOrder([FunctionTrigger] OrderProcessingRequest request)
    {
        Console.WriteLine($"🔧 Processing e-commerce order: {request.RequestId}");

        // Simulate order processing
        await Task.Delay(200);

        return $"E-commerce Order {request.RequestId} processed successfully with data: {request.Data}";
    }
}

/// <summary>
/// Request model for order processing.
/// </summary>
public class OrderProcessingRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Result model with typed input/output support.
/// </summary>
public class Result<TSuccess, TError>
{
    public bool IsSuccess { get; set; }
    public TSuccess? Success { get; set; }
    public TError? Error { get; set; }

    public static Result<TSuccess, TError> Ok(TSuccess success)
    {
        return new Result<TSuccess, TError> { IsSuccess = true, Success = success };
    }

    public static Result<TSuccess, TError> Fail(TError error)
    {
        return new Result<TSuccess, TError> { IsSuccess = false, Error = error };
    }
}