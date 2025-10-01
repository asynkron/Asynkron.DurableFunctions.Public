using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.AzureAdapter.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating how to use the new IOrchestrationClient (core) and IDurableOrchestrationClient (Azure adapter).
/// Shows both programmatic client usage patterns.
/// </summary>
public static class OrchestrationClientExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Orchestration Client Example ===\n");

        // Set up the host with dependency injection
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .ConfigureServices(services =>
            {
                // Register state store
                services.AddSingleton<IStateStore>(provider =>
                {
                    provider.GetRequiredService<ILogger<InMemoryStateStore>>();
                    return new InMemoryStateStore();
                });

                // Register durable function runtime
                services.AddSingleton<DurableFunctionRuntime>(provider =>
                {
                    var stateStore = provider.GetRequiredService<IStateStore>();
                    var logger = provider.GetRequiredService<ILogger<DurableFunctionRuntime>>();
                    
                    var runtime = new DurableFunctionRuntime(stateStore, logger);
                    
                    // Register orchestrators and activities
                    RegisterFunctions(runtime);
                    
                    return runtime;
                });

                // Add the orchestration management services and client
                services.AddDurableFunctionsManagementService();

                // Register the Azure adapter client for compatibility
                services.AddSingleton<IDurableOrchestrationClient>(provider =>
                {
                    var coreClient = provider.GetRequiredService<IOrchestrationClient>();
                    return new DurableOrchestrationClientAdapter(coreClient);
                });
            });

        using var host = hostBuilder.Build();
        await host.StartAsync();

        try
        {
            // Demonstrate using the core IOrchestrationClient
            await DemostrateCorClient(host.Services);

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Demonstrate using the Azure-compatible IDurableOrchestrationClient
            await DemonstrateAzureClient(host.Services);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task DemostrateCorClient(IServiceProvider services)
    {
        Console.WriteLine("Using Core IOrchestrationClient:");
        
        var client = services.GetRequiredService<IOrchestrationClient>();
        
        // Start a new orchestration
        Console.WriteLine("Starting Hello World orchestration...");
        var startResponse = await client.StartNewAsync("HelloWorldOrchestrator", "Alice");
        Console.WriteLine($"Started orchestration with Instance ID: {startResponse.InstanceId}");

        // Get the status
        var status = await client.GetStatusAsync(startResponse.InstanceId);
        Console.WriteLine($"Orchestration Status: {status?.RuntimeStatus}");
        Console.WriteLine($"Orchestration Name: {status?.Name}");
        Console.WriteLine($"Input: {status?.Input}");

        // Send an external event
        Console.WriteLine("Sending external event...");
        await client.RaiseEventAsync(startResponse.InstanceId, "UserInput", "Hello from client!");

        // Wait a bit and check status again
        await Task.Delay(500);
        status = await client.GetStatusAsync(startResponse.InstanceId);
        Console.WriteLine($"Updated Status: {status?.RuntimeStatus}");
        
        // Note: In a real scenario, you might wait for completion or poll status
        Console.WriteLine("Core client example completed.");
    }

    private static async Task DemonstrateAzureClient(IServiceProvider services)
    {
        Console.WriteLine("Using Azure-Compatible IDurableOrchestrationClient:");
        
        var client = services.GetRequiredService<IDurableOrchestrationClient>();
        
        // Start a new orchestration (Azure-style API)
        Console.WriteLine("Starting Hello World orchestration with custom instance ID...");
        var instanceId = await client.StartNewAsync("HelloWorldOrchestrator", "azure-example-1", "Bob");
        Console.WriteLine($"Started orchestration with Instance ID: {instanceId}");

        // Get the status (Azure-style)
        var status = await client.GetStatusAsync(instanceId);
        Console.WriteLine($"Orchestration Status: {status?.RuntimeStatus}");
        Console.WriteLine($"Orchestration Name: {status?.Name}");
        Console.WriteLine($"Input: {status?.Input}");
        
        // Start another orchestration without specifying instance ID
        Console.WriteLine("Starting another orchestration (auto-generated ID)...");
        var instanceId2 = await client.StartNewAsync("HelloWorldOrchestrator", "Charlie");
        Console.WriteLine($"Started orchestration with Instance ID: {instanceId2}");

        // Send an external event
        Console.WriteLine("Sending external event to first orchestration...");
        await client.RaiseEventAsync(instanceId, "UserInput", "Hello from Azure client!");

        Console.WriteLine("Azure client example completed.");
    }

    private static void RegisterFunctions(DurableFunctionRuntime runtime)
    {
        // Simple Hello World orchestrator
        runtime.RegisterJsonOrchestrator("HelloWorldOrchestrator", async (context, input) =>
        {
            var name = input?.ToString() ?? "World";
            
            // Call an activity to get the greeting
            var greeting = await context.CallAsync<string>("SayHelloActivity", name);
            
            return greeting;
        });

        // Simple activity function (registered as a regular function)
        runtime.RegisterJsonFunction("SayHelloActivity", (context, input) =>
        {
            var logger = context.GetLogger();
            // Input is a JSON string, so we need to parse it
            var name = "World";
            if (!string.IsNullOrEmpty(input))
            {
                // Remove quotes if it's a simple JSON string
                name = input.Trim('"');
            }
            var message = $"Hello, {name}! (from activity)";
            logger.LogInformation("Activity SayHelloActivity executing: {Message} for instance {InstanceId}", message, context.InstanceId);
            Console.WriteLine($"Activity executed: {message}");
            return Task.FromResult($"\"{message}\""); // Return as JSON string
        });
    }
}