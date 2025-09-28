using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Asynkron.DurableFunctions.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating how to set up a web API with built-in orchestration management endpoints.
/// This example shows how to integrate the HTTP APIs into an ASP.NET Core application.
/// </summary>
public static class WebApiExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Web API with Orchestration Management Example ===\n");

        var builder = WebApplication.CreateBuilder();

        // Configure logging
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Information);

        // Register state store
        builder.Services.AddSingleton<IStateStore>(provider =>
        {
            provider.GetRequiredService<ILogger<InMemoryStateStore>>();
            return new InMemoryStateStore();
        });

        // Register durable function runtime
        builder.Services.AddSingleton<DurableFunctionRuntime>(provider =>
        {
            var stateStore = provider.GetRequiredService<IStateStore>();
            var logger = provider.GetRequiredService<ILogger<DurableFunctionRuntime>>();
            
            var runtime = new DurableFunctionRuntime(stateStore, logger);
            
            // Register your orchestrators and activities
            RegisterFunctions(runtime);
            
            return runtime;
        });

        // Add the built-in orchestration management APIs
        builder.Services.AddDurableFunctionsManagement(options =>
        {
            options.BaseUrl = "https://localhost:5001"; // Your app's base URL
        });

        // Add controllers and other services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure middleware
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.UseDurableTraceContext();
        app.MapControllers();

        // Start the background processing
        var runtime = app.Services.GetRequiredService<DurableFunctionRuntime>();
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Start the runtime in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await runtime.RunAndPollAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Runtime stopped.");
            }
        });

        Console.WriteLine("🚀 Starting web server...");
        Console.WriteLine("📚 Swagger UI available at: https://localhost:5001/swagger");
        Console.WriteLine("\n📋 Available orchestration management endpoints:");
        Console.WriteLine("  POST   /runtime/orchestrations/start/{orchestratorName}");
        Console.WriteLine("  GET    /runtime/orchestrations/{instanceId}");
        Console.WriteLine("  POST   /runtime/orchestrations/{instanceId}/raiseEvent/{eventName}");
        Console.WriteLine("  POST   /runtime/orchestrations/{instanceId}/terminate");
        Console.WriteLine("  DELETE /runtime/orchestrations/{instanceId}");
        Console.WriteLine();
        
        // Example of how to use the APIs programmatically
        await DemonstrateApiUsage(runtime);

        Console.WriteLine("\n🎯 Try these curl commands:");
        PrintCurlExamples();

        // Keep the application running until Ctrl+C
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() => cancellationTokenSource.Cancel());

        try
        {
            await app.RunAsync();
        }
        finally
        {
            cancellationTokenSource.Cancel();
        }
    }

    private static void RegisterFunctions(DurableFunctionRuntime runtime)
    {
        // Register a simple activity
        runtime.RegisterFunction<string, string>("SayHello", async name =>
        {
            await Task.Delay(1000); // Simulate some work
            return $"Hello, {name}!";
        });

        // Register a simple orchestrator
        runtime.RegisterOrchestrator<string>("HelloOrchestrator", async context =>
        {
            var input = context.GetInput<string>();
            var greeting = await context.CallAsync<string>("SayHello", input);
            return $"Orchestrator says: {greeting}";
        });

        // Register an orchestrator that waits for events
        runtime.RegisterOrchestrator<string>("InteractiveOrchestrator", async context =>
        {
            var input = context.GetInput<string>();
            
            // Say hello first
            var greeting = await context.CallAsync<string>("SayHello", input);
            
            // Wait for an external event
            var eventData = await context.WaitForEvent<string>("UserInput");
            
            return $"{greeting} You said: {eventData}";
        });

        // Register a timer-based orchestrator
        runtime.RegisterOrchestrator<string>("DelayedOrchestrator", async context =>
        {
            var delaySeconds = context.GetInput<int>();
            
            var dueTime = context.CurrentUtcDateTime.AddSeconds(delaySeconds);
            await context.CreateTimer(dueTime);
            
            return $"Completed after {delaySeconds} seconds delay";
        });
    }

    private static async Task DemonstrateApiUsage(DurableFunctionRuntime runtime)
    {
        Console.WriteLine("🔄 Starting example orchestrations...");

        // Start a simple orchestrator
        await runtime.TriggerAsync("example", "HelloOrchestrator", "World");
        
        // Start an interactive orchestrator
        await runtime.TriggerAsync("example", "InteractiveOrchestrator", "Interactive User");
        
        // Start a delayed orchestrator
        await runtime.TriggerAsync("example", "DelayedOrchestrator", "5");

        Console.WriteLine("   ✅ Started 3 example orchestrations");
    }

    private static void PrintCurlExamples()
    {
        Console.WriteLine("# Start a new orchestration:");
        Console.WriteLine("curl -X POST \"https://localhost:5001/runtime/orchestrations/start/HelloOrchestrator\" \\");
        Console.WriteLine("     -H \"Content-Type: application/json\" \\");
        Console.WriteLine("     -d '{\"input\": \"API User\"}'");
        Console.WriteLine();

        Console.WriteLine("# Get orchestration status (replace {instanceId} with actual instance ID):");
        Console.WriteLine("curl -X GET \"https://localhost:5001/runtime/orchestrations/{instanceId}\"");
        Console.WriteLine();

        Console.WriteLine("# Send an event to an orchestration:");
        Console.WriteLine("curl -X POST \"https://localhost:5001/runtime/orchestrations/{instanceId}/raiseEvent/UserInput\" \\");
        Console.WriteLine("     -H \"Content-Type: application/json\" \\");
        Console.WriteLine("     -d '{\"eventData\": \"Hello from API!\"}'");
        Console.WriteLine();

        Console.WriteLine("# Terminate an orchestration:");
        Console.WriteLine("curl -X POST \"https://localhost:5001/runtime/orchestrations/{instanceId}/terminate\" \\");
        Console.WriteLine("     -H \"Content-Type: application/json\" \\");
        Console.WriteLine("     -d '{\"reason\": \"User requested termination\"}'");
        Console.WriteLine();

        Console.WriteLine("# Purge orchestration history:");
        Console.WriteLine("curl -X DELETE \"https://localhost:5001/runtime/orchestrations/{instanceId}\"");
    }
}
