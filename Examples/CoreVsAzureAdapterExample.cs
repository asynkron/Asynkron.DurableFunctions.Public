using Asynkron.DurableFunctions.AzureAdapter.Attributes;
using Asynkron.DurableFunctions.AzureAdapter.Core;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating the key differences between the Core API and Azure Adapter API.
/// This shows both approaches side by side for comparison.
/// </summary>
public static class CoreVsAzureAdapterExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Core vs Azure Adapter API Comparison ===\n");

        // Setup the runtime
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        Console.WriteLine("📚 This example demonstrates two different approaches:");
        Console.WriteLine("  1. Core API - Our native Asynkron.DurableFunctions API");
        Console.WriteLine("  2. Azure Adapter API - Azure Durable Functions compatibility layer");
        Console.WriteLine();

        // === CORE API REGISTRATION ===
        Console.WriteLine("🚀 Registering functions using CORE API:");
        Console.WriteLine("   - Uses RegisterFunction<TInput, TOutput>()");
        Console.WriteLine("   - Uses RegisterOrchestrator<TInput, TOutput>()");
        Console.WriteLine("   - Functions are called with context.CallAsync<T>()");
        Console.WriteLine();

        // Register core functions
        runtime.RegisterFunction<string, string>("CoreGreeting", async name =>
        {
            Console.WriteLine($"🔧 Core function executing for: {name}");
            await Task.Delay(100); // Simulate work
            return $"Hello from Core API, {name}!";
        });

        runtime.RegisterFunction<string, string>("CoreProcessing", async data =>
        {
            Console.WriteLine($"🔧 Core processing function executing with: {data}");
            await Task.Delay(150); // Simulate work
            return $"Processed by Core: {data}";
        });

        // Register core orchestrator
        runtime.RegisterOrchestrator<string, string>("CoreOrchestrator", async (context, input) =>
        {
            Console.WriteLine($"🎯 Core orchestrator started with input: {input}");
            
            // Using the core API: CallAsync<T>()
            var greeting = await context.CallAsync<string>("CoreGreeting", input);
            var processed = await context.CallAsync<string>("CoreProcessing", greeting);
            
            return $"Core result: {processed}";
        });

        // === AZURE ADAPTER API REGISTRATION ===
        Console.WriteLine("🔄 Registering functions using AZURE ADAPTER API:");
        Console.WriteLine("   - Uses Azure-style attributes [FunctionName]");
        Console.WriteLine("   - Uses Azure-style triggers [DurableOrchestrationTrigger], [DurableActivityTrigger]");
        Console.WriteLine("   - Functions are called with context.CallActivityAsync<T>()");
        Console.WriteLine();

        // Register Azure-style functions using the adapter
        var azureFunctions = new AzureStyleFunctions();
        runtime.RegisterAzureFunctionsFromType(typeof(AzureStyleFunctions), azureFunctions);

        Console.WriteLine("✅ Both API styles registered successfully!");
        Console.WriteLine();

        // === RUN CORE ORCHESTRATOR ===
        Console.WriteLine("=" + new string('=', 60));
        Console.WriteLine("🚀 Running CORE API example:");
        Console.WriteLine("=" + new string('=', 60));
        
        var coreInstanceId = Guid.NewGuid().ToString();
        await runtime.TriggerAsyncObject(coreInstanceId, "CoreOrchestrator", "World (Core)");

        // === RUN AZURE ADAPTER ORCHESTRATOR ===
        Console.WriteLine();
        Console.WriteLine("=" + new string('=', 60));
        Console.WriteLine("🔄 Running AZURE ADAPTER API example:");
        Console.WriteLine("=" + new string('=', 60));
        
        var azureInstanceId = Guid.NewGuid().ToString();
        await runtime.TriggerAsyncObject(azureInstanceId, "AzureOrchestrator", "World (Azure)");

        // Run the runtime to execute both workflows
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }

        Console.WriteLine();
        Console.WriteLine("=" + new string('=', 60));
        Console.WriteLine("🎉 Example Complete - Key Differences Summary:");
        Console.WriteLine("=" + new string('=', 60));
        Console.WriteLine();
        
        Console.WriteLine("📋 CORE API (Asynkron.DurableFunctions):");
        Console.WriteLine("   ✓ Native C# registration: runtime.RegisterFunction<T,U>()");
        Console.WriteLine("   ✓ Native method calls: context.CallAsync<T>()");
        Console.WriteLine("   ✓ Clean, simple API design");
        Console.WriteLine("   ✓ No attributes required");
        Console.WriteLine();
        
        Console.WriteLine("🔄 AZURE ADAPTER API (Compatibility Layer):");
        Console.WriteLine("   ✓ Azure-style attributes: [FunctionName], [DurableOrchestrationTrigger]");
        Console.WriteLine("   ✓ Azure-style method calls: context.CallActivityAsync<T>()");
        Console.WriteLine("   ✓ Drop-in replacement for Azure Durable Functions");
        Console.WriteLine("   ✓ Easy migration from Azure");
        Console.WriteLine();
        
        Console.WriteLine("💡 Choose Core API for new projects, Azure Adapter for migrations!");
    }
}

/// <summary>
/// Example Azure-compatible functions using the adapter.
/// Notice the Azure-style attributes and parameter types.
/// </summary>
public class AzureStyleFunctions
{
    /// <summary>
    /// Azure-compatible orchestrator function.
    /// Uses Azure attributes and CallActivityAsync method.
    /// </summary>
    [FunctionName("AzureOrchestrator")]
    public async Task<string> RunAzureOrchestrator([DurableOrchestrationTrigger] IDurableOrchestrationContext context)
    {
        var input = context.GetInput<string>();
        Console.WriteLine($"🎯 Azure orchestrator started with input: {input}");
        
        // Using the Azure Adapter API: CallActivityAsync<T>()
        var greeting = await context.CallActivityAsync<string>("AzureGreeting", input);
        var processed = await context.CallActivityAsync<string>("AzureProcessing", greeting);
        
        return $"Azure result: {processed}";
    }

    /// <summary>
    /// Azure-compatible activity function.
    /// Uses Azure attributes and context parameter.
    /// </summary>
    [FunctionName("AzureGreeting")]
    public async Task<string> AzureGreeting([DurableActivityTrigger] IDurableActivityContext context)
    {
        var name = context.GetInput<string>();
        Console.WriteLine($"🔧 Azure activity executing for: {name}");
        await Task.Delay(100); // Simulate work
        return $"Hello from Azure Adapter, {name}!";
    }

    /// <summary>
    /// Azure-compatible activity function.
    /// Uses Azure attributes and context parameter.
    /// </summary>
    [FunctionName("AzureProcessing")]
    public async Task<string> AzureProcessing([DurableActivityTrigger] IDurableActivityContext context)
    {
        var data = context.GetInput<string>();
        Console.WriteLine($"🔧 Azure processing activity executing with: {data}");
        await Task.Delay(150); // Simulate work
        return $"Processed by Azure Adapter: {data}";
    }
}