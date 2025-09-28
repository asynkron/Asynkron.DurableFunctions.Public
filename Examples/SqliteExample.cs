using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating the SQLite StateStore and enhanced ActivityResults tracking.
/// </summary>
public class SqliteExample
{
    public static async Task RunExample()
    {
        // Setup logging
        using var loggerFactory =
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();

        // Create SQLite StateStore (in-memory for demo)
        var connectionString = "Data Source=:memory:";
        using var stateStore = new SqliteStateStore(connectionString, loggerFactory.CreateLogger<SqliteStateStore>());

        // Create runtime
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Register activities
        runtime.RegisterFunction<string, string>("ProcessData", async input =>
        {
            await Task.Delay(1000); // Simulate work
            return $"Processed: {input}";
        });

        runtime.RegisterFunction<string, string>("TransformData", async input =>
        {
            await Task.Delay(500); // Simulate work
            return $"Transformed: {input}";
        });

        // Register orchestrator that shows parallel execution tracking
        runtime.RegisterJsonOrchestrator("ParallelProcessingOrchestrator", async (context, _) =>
        {
            Console.WriteLine("Starting parallel activities...");

            // Start multiple activities in parallel
            var task1 = context.CallAsync<string>("ProcessData", "data1");
            var task2 = context.CallAsync<string>("ProcessData", "data2");
            var task3 = context.CallAsync<string>("TransformData", "data3");

            // Wait for completion
            var results = await Task.WhenAll(task1, task2, task3);

            return $"Combined: {string.Join(", ", results)}";
        });

        // Start the orchestrator
        await runtime.TriggerAsync("root", "ParallelProcessingOrchestrator");

        // Run for a limited time
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var pollingTask = runtime.RunAndPollAsync(cts.Token);

        // Demonstrate querying state for debugging
        await Task.Delay(2000); // Let some activities start

        Console.WriteLine("\n=== Current State Information ===");
        var allStates = await stateStore.GetReadyStatesAsync(DateTimeOffset.MaxValue);

        foreach (var state in allStates)
        {
            Console.WriteLine($"State: {state.InstanceId[..8]}... ({state.FunctionName})");

            foreach (var activity in state.Entries)
            {
                var value = activity.Value;
                var status = value.IsCompleted ? "COMPLETED" : "IN PROGRESS";
                var duration = value.IsCompleted
                    ? $"(took {(DateTimeOffset.UtcNow - value.InitiatedAt).TotalMilliseconds:F0}ms)"
                    : $"(running for {(DateTimeOffset.UtcNow - value.InitiatedAt).TotalMilliseconds:F0}ms)";

                Console.WriteLine($"  Activity: {value.FunctionName} - {status} {duration}");
                Console.WriteLine($"    Input: {value.Input}");
                if (value.IsCompleted)
                {
                    Console.WriteLine($"    Result: {value.Result}");
                }
            }
        }

        // Wait for completion
        await Task.Delay(8000);
        cts.Cancel();

        try
        {
            await pollingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Console.WriteLine("\n=== Final State Count ===");
        Console.WriteLine($"Total states remaining: {await stateStore.GetCountAsync()}");
    }
}