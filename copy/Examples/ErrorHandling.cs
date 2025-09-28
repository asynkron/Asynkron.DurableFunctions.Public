using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

/// <summary>
/// Error handling and retry example showing resilience patterns
/// </summary>
public class ErrorHandlingExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Register resilient orchestrator
        runtime.RegisterOrchestratorFunction<string, string>("ResilientOrchestrator", async context =>
        {
            var data = context.GetInput<string>();
            
            try
            {
                // This might fail, but will retry automatically
                var result = await context.CallFunction<string>("UnreliableFunction", data);
                return $"Success: {result}";
            }
            catch (Exception ex)
            {
                // Handle failure after all retries exhausted
                await context.CallFunction("LogError", ex.Message);
                return "Failed after retries";
            }
        });

        // Register unreliable function that fails sometimes
        runtime.RegisterFunction<string, string>("UnreliableFunction", async data =>
        {
            Console.WriteLine($"🎲 Attempting to process: {data}");
            
            // Simulate 70% failure rate
            if (Random.Shared.NextDouble() < 0.7)
            {
                Console.WriteLine($"❌ Simulated failure for: {data}");
                throw new InvalidOperationException("Simulated failure!");
            }
            
            await Task.Delay(100);
            Console.WriteLine($"✅ Successfully processed: {data}");
            return $"Successfully processed: {data}";
        });

        // Register error logging function
        runtime.RegisterFunction<string, string>("LogError", async errorMessage =>
        {
            Console.WriteLine($"🚨 Error logged: {errorMessage}");
            await Task.Delay(50);
            return "Error logged";
        });

        // Example with graceful degradation
        runtime.RegisterOrchestratorFunction<string, string>("GracefulDegradationOrchestrator", async context =>
        {
            var input = context.GetInput<string>();
            
            try
            {
                // Try primary service
                return await context.CallFunction<string>("PrimaryService", input);
            }
            catch (Exception)
            {
                Console.WriteLine("⚠️ Primary service failed, trying fallback...");
                try
                {
                    // Fallback to secondary service
                    return await context.CallFunction<string>("FallbackService", input);
                }
                catch (Exception)
                {
                    Console.WriteLine("⚠️ Fallback service also failed, using default...");
                    return await context.CallFunction<string>("DefaultResponse", input);
                }
            }
        });

        // Register service functions
        runtime.RegisterFunction<string, string>("PrimaryService", async input =>
        {
            // Always fails for demo
            throw new InvalidOperationException("Primary service unavailable");
        });

        runtime.RegisterFunction<string, string>("FallbackService", async input =>
        {
            Console.WriteLine($"🔄 Using fallback service for: {input}");
            await Task.Delay(200);
            return $"Fallback result for: {input}";
        });

        runtime.RegisterFunction<string, string>("DefaultResponse", async input =>
        {
            Console.WriteLine($"🛡️ Using default response for: {input}");
            await Task.Delay(50);
            return $"Default response for: {input}";
        });

        // Run the examples
        Console.WriteLine("Starting error handling examples...");
        
        await runtime.TriggerAsync("resilient-001", "ResilientOrchestrator", "test-data");
        await runtime.TriggerAsync("graceful-001", "GracefulDegradationOrchestrator", "important-data");
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await runtime.RunAndPollAsync(cts.Token);
    }
}