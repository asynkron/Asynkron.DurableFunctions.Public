using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

/// <summary>
/// Data pipeline example showing multi-step data processing workflow
/// </summary>
public class DataPipelineExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Register a complete data pipeline workflow using CallFunction
        runtime.RegisterOrchestratorFunction<string, string>("DataPipelineOrchestrator", async context =>
        {
            var data = context.GetInput<string>();
            
            // Step 1: Validate
            var validated = await context.CallFunction<string>("ValidateData", data);
            
            // Step 2: Transform  
            var transformed = await context.CallFunction<string>("TransformData", validated);
            
            // Step 3: Store
            var result = await context.CallFunction<string>("StoreData", transformed);
            
            return $"Pipeline complete: {result}";
        });

        // Register functions
        runtime.RegisterFunction<string, string>("ValidateData", async data => 
        {
            Console.WriteLine($"🔍 Validating: {data}");
            await Task.Delay(100);
            return $"validated-{data}";
        });

        runtime.RegisterFunction<string, string>("TransformData", async data =>
        {
            Console.WriteLine($"🔄 Transforming: {data}");  
            await Task.Delay(200);
            return $"transformed-{data}";
        });

        runtime.RegisterFunction<string, string>("StoreData", async data =>
        {
            Console.WriteLine($"💾 Storing: {data}");
            await Task.Delay(150);
            return $"stored-{data}";
        });

        // Run the example
        await runtime.TriggerAsync("pipeline-001", "DataPipelineOrchestrator", "sample-data");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await runtime.RunAndPollAsync(cts.Token);
    }
}