using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

public static class PostgreSqlExample
{
    public static async Task RunAsync()
    {
        // PostgreSQL connection string - adjust as needed
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING") 
                              ?? "Host=localhost;Port=5432;Database=durablefunctions;Username=durableuser;Password=durablepass;Include Error Detail=true;";

        // Create logger factory
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Create PostgreSQL state store
        var stateStore = new PostgreSqlStateStore(connectionString, 
            loggerFactory.CreateLogger<PostgreSqlStateStore>());

        // Create runtime
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Register a simple activity function
        runtime.RegisterFunction<int, string>("GreetingActivity", async (input) =>
        {
            await Task.Delay(100); // Simulate some work
            return $"Hello from PostgreSQL! Input was: {input}";
        });

        // Register a simple orchestrator
        runtime.RegisterOrchestrator<string>("PostgreSqlOrchestrator", async (context) =>
        {
            var result1 = await context.CallAsync<string>("GreetingActivity", 42);
            var result2 = await context.CallAsync<string>("GreetingActivity", 100);
            
            return $"Orchestrator completed! Results: [{result1}] and [{result2}]";
        });

        Console.WriteLine("🐘 PostgreSQL Durable Functions Example");
        Console.WriteLine("======================================");
        Console.WriteLine();

        // Start the orchestrator
        Console.WriteLine("Starting PostgreSQL orchestrator...");
        var instanceId = Guid.NewGuid().ToString();
        await runtime.TriggerAsync(instanceId, "PostgreSqlOrchestrator", "input-data");
        Console.WriteLine($"Started orchestrator with instance ID: {instanceId}");

        // Start polling
        Console.WriteLine("Starting runtime polling...");
        var pollingTask = runtime.RunAndPollAsync(CancellationToken.None);

        // Wait a bit for execution
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Check the result
        var state = await stateStore.GetStateAsync(instanceId);
        if (state?.IsCompleted == true)
        {
            Console.WriteLine($"✅ Orchestrator completed successfully!");
            Console.WriteLine($"📋 Result: {state.CompletedResult}");
        }
        else
        {
            Console.WriteLine("⏳ Orchestrator still running or failed...");
        }

        Console.WriteLine();
        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("Example completed. Press any key to exit...");
            Console.ReadKey();
        }
        else
        {
            Console.WriteLine("Example completed.");
        }

        // Clean up
        await stateStore.DisposeAsync();
    }
}
