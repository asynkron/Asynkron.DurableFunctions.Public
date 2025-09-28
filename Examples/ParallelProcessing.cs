using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

/// <summary>
/// Parallel processing example showing fan-out/fan-in pattern
/// </summary>
public class ParallelProcessingExample
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
        runtime.RegisterOrchestratorFunction<string[], string>("ParallelProcessingOrchestrator", async context =>
        {
            var inputs = context.GetInput<string[]>();
            
            // Fan-out: Start all functions in parallel
            var tasks = inputs.Select(input => 
                context.CallFunction<string>("ProcessData", input)
            ).ToArray();
            
            // Fan-in: Wait for all to complete
            var results = await Task.WhenAll(tasks);
            
            return $"Processed {results.Length} items: {string.Join(", ", results)}";
        });

        // Register the activity function
        runtime.RegisterFunction<string, string>("ProcessData", async data =>
        {
            Console.WriteLine($"Processing {data}...");
            await Task.Delay(Random.Shared.Next(500, 1500)); // Simulate variable work
            return $"Processed-{data}";
        });

        // Run the example
        var inputData = new[] { "data1", "data2", "data3", "data4", "data5" };
        
        await runtime.TriggerAsync("parallel-001", "ParallelProcessingOrchestrator", inputData);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await runtime.RunAndPollAsync(cts.Token);
    }
}