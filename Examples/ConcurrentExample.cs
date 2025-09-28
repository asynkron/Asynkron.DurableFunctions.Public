using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating multi-host orchestrator concurrency with lease-based execution.
/// This example shows how to safely run multiple hosts against the same database using the
/// unified SqliteStateStore which provides concurrent orchestrator execution by default.
/// </summary>
public class ConcurrentExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Multi-Host Orchestrator Concurrency Example ===\n");

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Simulate two different hosts running concurrently
        var host1Task = RunHost("host-1", loggerFactory);
        var host2Task = RunHost("host-2", loggerFactory);

        // Let them run for a while
        await Task.Delay(10000);

        Console.WriteLine("\nStopping hosts...");

        // Wait for both hosts to complete
        await Task.WhenAll(host1Task, host2Task);
    }

    private static async Task RunHost(string hostId, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var stateStoreLogger = loggerFactory.CreateLogger<SqliteStateStore>();

        Console.WriteLine($"Starting {hostId}...");

        // Create unified state store with concurrent capabilities (shared database file)
        var stateStore = new SqliteStateStore(
            "Data Source=concurrent_example.db;Cache=Shared;Journal Mode=WAL;",
            stateStoreLogger);

        // Create runtime - all state stores now support concurrent orchestrator execution
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Register functions
        RegisterFunctions(runtime);

        // Only host-1 will start the orchestrators
        if (hostId == "host-1")
        {
            await StartOrchestrators(runtime);
        }

        // Start the polling loop with cancellation after 15 seconds
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"{hostId} stopped.");
        }
        finally
        {
            stateStore.Dispose();
        }
    }

    private static void RegisterFunctions(DurableFunctionRuntime runtime)
    {
        // Register activities (these don't need concurrency control)
        runtime.RegisterFunction<string, string>("ProcessData", async input =>
        {
            Console.WriteLine($"[{runtime.HostId}] Processing data: {input}");
            await Task.Delay(1000); // Simulate work
            return $"Processed by {runtime.HostId}: {input}";
        });

        runtime.RegisterFunction<string, string>("ValidateData", async input =>
        {
            Console.WriteLine($"[{runtime.HostId}] Validating data: {input}");
            await Task.Delay(500); // Simulate validation
            return $"Validated by {runtime.HostId}: {input}";
        });

        runtime.RegisterFunction<string, string>("SaveData", async input =>
        {
            Console.WriteLine($"[{runtime.HostId}] Saving data: {input}");
            await Task.Delay(300); // Simulate database save
            return $"Saved by {runtime.HostId}: {input}";
        });

        // Register orchestrators (these use concurrency control)
        runtime.RegisterJsonOrchestrator("DataProcessingOrchestrator", async (context, input) =>
        {
            var data = !string.IsNullOrEmpty(input) ? input : "default";
            Console.WriteLine($"[{runtime.HostId}] Started orchestrator for: {data}");

            try
            {
                // Step 1: Process the data
                var processed = await context.CallAsync<string>("ProcessData", data);

                // Step 2: Validate the processed data
                var validated = await context.CallAsync<string>("ValidateData", processed);

                // Step 3: Save the validated data
                var saved = await context.CallAsync<string>("SaveData", validated);

                Console.WriteLine($"[{runtime.HostId}] Completed orchestrator: {saved}");
                return saved;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{runtime.HostId}] Orchestrator failed: {ex.Message}");
                throw;
            }
        });

        // A long-running orchestrator to demonstrate lease renewal
        runtime.RegisterJsonOrchestrator("LongRunningOrchestrator", async (context, input) =>
        {
            var data = !string.IsNullOrEmpty(input) ? input : "default";
            Console.WriteLine($"[{runtime.HostId}] Started long-running orchestrator for: {data}");

            // Simulate multiple steps with delays
            for (var i = 1; i <= 5; i++)
            {
                var stepData = $"{data}-step-{i}";
                var result = await context.CallAsync<string>("ProcessData", stepData);
                Console.WriteLine($"[{runtime.HostId}] Completed step {i}: {result}");

                // Add delay between steps to test lease renewal
                await Task.Delay(2000);
            }

            var finalResult = $"Long processing complete by {runtime.HostId}: {data}";
            Console.WriteLine($"[{runtime.HostId}] Long-running orchestrator completed: {finalResult}");
            return finalResult;
        });
    }

    private static async Task StartOrchestrators(DurableFunctionRuntime runtime)
    {
        Console.WriteLine($"[{runtime.HostId}] Starting orchestrators...\n");

        // Start several quick orchestrators
        for (var i = 1; i <= 5; i++)
            await runtime.TriggerAsync("demo", "DataProcessingOrchestrator", $"quick-data-{i}");

        // Start a couple of long-running orchestrators
        for (var i = 1; i <= 2; i++) await runtime.TriggerAsync("demo", "LongRunningOrchestrator", $"long-data-{i}");

        Console.WriteLine($"[{runtime.HostId}] Started 7 orchestrators total\n");
    }
}

/// <summary>
/// Entry point for running the concurrent example.
/// Run this from a different main method or remove to avoid conflict.
/// </summary>
/*
public class ConcurrentExampleProgram
{
    public static async Task Main(string[] args)
    {
        try
        {
            await ConcurrentExample.RunExample();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
*/