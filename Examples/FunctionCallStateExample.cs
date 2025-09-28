using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating the function call state foundation that enables idempotent function invocation.
/// This shows the core Asynkron.DurableFunctions API pattern:
/// 
/// try
/// {
///     var x = await context.CallAsync<string>("F1", null);
///     var y = await context.CallAsync<string>("F2", x);
///     var z = await context.CallAsync<string>("F3", y);
///     return await context.CallAsync<string>("F4", z);
/// }
/// catch (Exception)
/// {
///     // Error handling or compensation goes here.
/// }
/// 
/// Note: The core API uses CallAsync() method, while the Azure Adapter uses CallActivityAsync().
/// </summary>
public static class FunctionCallStateExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Function Call State Foundation Example ===\n");

        // Setup the runtime
        var stateStore = new InMemoryStateStore();
        using var loggerFactory =
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Register activities F1, F2, F3, F4
        runtime.RegisterJsonFunction("F1", async (_, input) =>
        {
            Console.WriteLine($"🔧 F1 executing with input: {input}");
            await Task.Delay(100); // Simulate work
            return "Result from F1";
        });

        runtime.RegisterJsonFunction("F2", async (_, input) =>
        {
            Console.WriteLine($"🔧 F2 executing with input: {input}");
            await Task.Delay(100); // Simulate work
            return $"Result from F2 (processed {input})";
        });

        runtime.RegisterJsonFunction("F3", async (_, input) =>
        {
            Console.WriteLine($"🔧 F3 executing with input: {input}");
            await Task.Delay(100); // Simulate work
            return $"Result from F3 (processed {input})";
        });

        runtime.RegisterJsonFunction("F4", async (_, input) =>
        {
            Console.WriteLine($"🔧 F4 executing with input: {input}");
            await Task.Delay(100); // Simulate work
            return $"FINAL: {input}";
        });

        // Register the orchestrator that demonstrates the pattern
        runtime.RegisterJsonOrchestrator("ChainedWorkflow", async (context, _) =>
        {
            Console.WriteLine("🎯 Orchestrator started - beginning activity chain...");

            try
            {
                // This is the exact pattern from the problem statement
                var x = await context.CallAsync<string>("F1");
                Console.WriteLine($"✅ Got x: {x}");

                var y = await context.CallAsync<string>("F2", x);
                Console.WriteLine($"✅ Got y: {y}");

                var z = await context.CallAsync<string>("F3", y);
                Console.WriteLine($"✅ Got z: {z}");

                var result = await context.CallAsync<string>("F4", z);
                Console.WriteLine($"🎉 Final result: {result}");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in orchestrator: {ex.Message}");
                // Error handling or compensation would go here
                return $"Failed: {ex.Message}";
            }
        });

        // Trigger the orchestration
        Console.WriteLine("🚀 Triggering ChainedWorkflow orchestration...\n");
        await runtime.TriggerAsync("abc", "ChainedWorkflow");

        // Run the runtime to execute the workflow
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var pollingTask = runtime.RunAndPollAsync(cts.Token);

        // Wait for completion
        await Task.Delay(10000);
        cts.Cancel();

        try
        {
            await pollingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }

        Console.WriteLine("\n=== Example Complete ===");
        Console.WriteLine($"📊 Final state count: {stateStore.Count}");
        Console.WriteLine();
        Console.WriteLine("🔍 Key Features Demonstrated:");
        Console.WriteLine("✓ Idempotent activity invocation hashing");
        Console.WriteLine("✓ WaitingForStateUpdateException orchestrator pausing");
        Console.WriteLine("✓ Activity result caching and propagation");
        Console.WriteLine("✓ Sequential activity execution with result chaining");
        Console.WriteLine("✓ Automatic orchestrator resumption after activity completion");
    }
}