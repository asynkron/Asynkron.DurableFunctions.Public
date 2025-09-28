using Asynkron.DurableFunctions.AzureAdapter.Attributes;
using Asynkron.DurableFunctions.AzureAdapter.Core;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating Azure Durable Functions compatibility using the AzureAdapter.
/// This shows how existing Azure Durable Functions code can work with minimal changes.
/// </summary>
public class AzureCompatibilityExample
{
    /// <summary>
    /// An Azure-compatible orchestrator function that calls multiple activities in sequence.
    /// Uses Azure-style attributes and interfaces for compatibility.
    /// </summary>
    /// <param name="context">The Azure-compatible durable orchestration context.</param>
    /// <returns>A task representing the orchestration workflow.</returns>
    [FunctionName("AzureStyleOrchestrator")]
    public async Task<string> RunOrchestrator([DurableOrchestrationTrigger] IDurableOrchestrationContext context)
    {
        // This code looks exactly like Azure Durable Functions!
        
        // Call the first activity
        var result1 = await context.CallActivityAsync<string>("AzureSayHello", "Tokyo");
        
        // Call the second activity
        var result2 = await context.CallActivityAsync<string>("AzureSayHello", "Seattle");
        
        // Call the third activity  
        var result3 = await context.CallActivityAsync<string>("AzureSayHello", "London");
        
        // Return the combined results
        return $"{result1}, {result2}, {result3}";
    }

    /// <summary>
    /// An Azure-compatible activity function that would be called by the orchestrator.
    /// Uses Azure-style attributes and interfaces for compatibility.
    /// </summary>
    /// <param name="context">The Azure-compatible durable activity context.</param>
    /// <returns>A greeting message.</returns>
    [FunctionName("AzureSayHello")]
    public string SayHello([DurableActivityTrigger] IDurableActivityContext context)
    {
        var name = context.GetInput<string>();
        return $"Hello {name}!";
    }

    /// <summary>
    /// Alternative activity function that takes input directly instead of through context.
    /// This is also Azure-compatible.
    /// </summary>
    /// <param name="name">The name to greet.</param>
    /// <returns>A greeting message.</returns>
    [FunctionName("AzureDirectSayHello")]
    public string DirectSayHello(string name)
    {
        return $"Hello {name} directly!";
    }

    /// <summary>
    /// An Azure-compatible sub-orchestrator function that processes multiple names.
    /// Demonstrates the new CallSubOrchestratorAsync functionality.
    /// </summary>
    /// <param name="context">The Azure-compatible durable orchestration context.</param>
    /// <returns>A task representing the sub-orchestration workflow.</returns>
    [FunctionName("AzureSubOrchestrator")]
    public async Task<string> RunSubOrchestrator([DurableOrchestrationTrigger] IDurableOrchestrationContext context)
    {
        var names = context.GetInput<string[]>();
        var results = new List<string>();

        foreach (var name in names)
        {
            var greeting = await context.CallActivityAsync<string>("AzureSayHello", name);
            results.Add(greeting);
        }

        return string.Join(", ", results);
    }

    /// <summary>
    /// A parent orchestrator that calls sub-orchestrators using the new CallSubOrchestratorAsync method.
    /// This demonstrates Azure-compatible sub-orchestrator functionality.
    /// </summary>
    /// <param name="context">The Azure-compatible durable orchestration context.</param>
    /// <returns>A task representing the parent orchestration workflow.</returns>
    [FunctionName("AzureParentOrchestrator")]
    public async Task<string> RunParentOrchestrator([DurableOrchestrationTrigger] IDurableOrchestrationContext context)
    {
        // Call sub-orchestrators using the new CallSubOrchestratorAsync method
        var asianCities = new[] { "Tokyo", "Seoul", "Bangkok" };
        var europeanCities = new[] { "London", "Paris", "Berlin" };
        
        // Call sub-orchestrators in parallel
        var asianTask = context.CallSubOrchestratorAsync<string>("AzureSubOrchestrator", asianCities);
        var europeanTask = context.CallSubOrchestratorAsync<string>("AzureSubOrchestrator", europeanCities);

        var asianResults = await asianTask;
        var europeanResults = await europeanTask;

        return $"Asian: {asianResults}; European: {europeanResults}";
    }

    /// <summary>
    /// Runs the Azure compatibility example to demonstrate the adapter in action.
    /// </summary>
    public static async Task RunExample()
    {
        Console.WriteLine("=== Azure Durable Functions Compatibility Example ===\n");

        // Setup the Asynkron runtime (not Azure!)
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger);

        // Register Azure-style functions using the adapter
        var azureFunctions = new AzureCompatibilityExample();
        runtime.RegisterAzureFunctionsFromType(typeof(AzureCompatibilityExample), azureFunctions);

        Console.WriteLine("✅ Registered Azure-compatible functions:");
        Console.WriteLine("   - AzureStyleOrchestrator (orchestrator)");
        Console.WriteLine("   - AzureSayHello (activity)");
        Console.WriteLine("   - AzureDirectSayHello (activity)");
        Console.WriteLine();

        // Start the orchestrator
        var instanceId = Guid.NewGuid().ToString();
        await runtime.TriggerAsync(instanceId, "AzureStyleOrchestrator", null, DateTimeOffset.UtcNow);

        Console.WriteLine($"🚀 Started orchestrator with instance ID: {instanceId}");
        Console.WriteLine("⏳ Running orchestration...");
        Console.WriteLine();

        // Run the polling loop for a few seconds to let it complete
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Orchestration completed!");
        }

        Console.WriteLine();
        Console.WriteLine("🎉 This Azure Durable Functions code ran on Asynkron.DurableFunctions!");
        Console.WriteLine("📦 No Azure dependencies required - runs anywhere!");
        Console.WriteLine();
    }
}