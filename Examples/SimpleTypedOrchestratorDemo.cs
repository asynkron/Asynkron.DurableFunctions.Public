using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Simple demonstration of the new typed orchestrator functionality.
/// Shows the core features: RegisterOrchestratorFunction<TIn, TOut>, GetInput<T>(), and GetLogger().
/// </summary>
public static class SimpleTypedOrchestratorDemo
{
    public class GreetingRequest
    {
        public string Name { get; set; } = "";
        public string Language { get; set; } = "English";
    }

    public class GreetingResult
    {
        public string Message { get; set; } = "";
        public string Language { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public static async Task RunDemo()
    {
        Console.WriteLine("=== Simple Typed Orchestrator Demo ===\n");

        // Setup
        var stateStore = new InMemoryStateStore();
        using var loggerFactory =
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Register a simple activity
        runtime.RegisterFunction<string, string>("FormatGreeting", async (name) =>
        {
            await Task.Delay(100); // Simulate work
            return $"Hello, {name}! Welcome to Asynkron.DurableFunctions.";
        });

        // Register typed orchestrator - this is the new functionality!
        runtime.RegisterOrchestrator<GreetingRequest, GreetingResult>("CreateGreeting",
            async (context, request) =>
            {
                // Use GetLogger() - another new feature!
                var orchestratorLogger = context.GetLogger();
                orchestratorLogger.LogInformation("🎯 Creating greeting for {Name} in {Language}", request.Name,
                    request.Language);

                // Call an activity
                var formattedMessage = await context.CallAsync<string>("FormatGreeting", request.Name);

                orchestratorLogger.LogInformation("✅ Greeting created successfully");

                // Return typed result
                return new GreetingResult
                {
                    Message = formattedMessage,
                    Language = request.Language,
                    CreatedAt = DateTime.UtcNow
                };
            });

        // Test with typed input
        var greetingRequest = new GreetingRequest
        {
            Name = "Alice",
            Language = "English"
        };

        Console.WriteLine("🚀 Triggering typed orchestrator...\n");
        await runtime.TriggerAsyncObject("abc", "CreateGreeting", greetingRequest);

        // Execute
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Demo completed!\n");
        }

        Console.WriteLine("🔍 New Features Demonstrated:");
        Console.WriteLine("✓ RegisterOrchestratorFunction<TIn, TOut> - typed input/output");
        Console.WriteLine("✓ context.GetInput<T>() - automatic input deserialization");
        Console.WriteLine("✓ context.GetLogger() - access to logger instance");
        Console.WriteLine("✓ Consistent with activity registration patterns");
    }
}