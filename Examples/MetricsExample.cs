using System.Diagnostics.Metrics;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Monitoring;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Demonstrates how to configure and use Durable Functions with metrics enabled.
/// </summary>
public class MetricsExample
{
    public static async Task RunAsync()
    {
        // Create a host with logging and metrics
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Register Durable Functions components
                services.AddSingleton<IStateStore, InMemoryStateStore>();
                services.AddSingleton<DurableFunctionsMetrics>();
                services.AddSingleton<DurableFunctionRuntime>(serviceProvider =>
                {
                    var stateStore = serviceProvider.GetRequiredService<IStateStore>();
                    var logger = serviceProvider.GetRequiredService<ILogger<DurableFunctionRuntime>>();
                    var metrics = serviceProvider.GetRequiredService<DurableFunctionsMetrics>();
                    
                    return new DurableFunctionRuntime(stateStore, logger, metrics: metrics);
                });

                // Add OpenTelemetry (optional - requires OpenTelemetry packages)
                // services.AddOpenTelemetry()
                //     .WithMetrics(builder => builder
                //         .AddMeter("Asynkron.DurableFunctions")
                //         .AddConsoleExporter());
            })
            .Build();

        var runtime = host.Services.GetRequiredService<DurableFunctionRuntime>();
        var logger = host.Services.GetRequiredService<ILogger<MetricsExample>>();

        // Register functions and orchestrators
        RegisterFunctions(runtime);

        logger.LogInformation("Starting Durable Functions metrics example...");

        // Trigger some orchestrations to generate metrics
        var orchestrationTasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            var instanceId = $"metrics-demo-{i}";
            orchestrationTasks.Add(runtime.TriggerAsync(instanceId, "DemoOrchestrator", $"{{\"iteration\": {i}}}"));
        }

        // Start the runtime polling
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pollingTask = runtime.RunAndPollAsync(cancellationTokenSource.Token);

        // Wait for orchestrations to complete
        await Task.WhenAll(orchestrationTasks);
        await Task.Delay(2000); // Allow processing time

        // Trigger some events
        await runtime.RaiseEventAsync("metrics-demo-0", "UserInput", new { value = "test" });
        await runtime.RaiseEventAsync("metrics-demo-1", "UserInput", new { value = "example" });

        await Task.Delay(1000); // Allow event processing

        logger.LogInformation("Metrics example completed. Check your metrics collector for data.");

        // Cancel polling and shutdown
        cancellationTokenSource.Cancel();
        try
        {
            await pollingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }
    }

    private static void RegisterFunctions(DurableFunctionRuntime runtime)
    {
        // Register a simple orchestrator
        runtime.RegisterJsonOrchestrator("DemoOrchestrator", async (context, input) =>
        {
            Console.WriteLine($"Demo orchestrator started with input: {input}");

            // Call some activities
            var result1 = await context.CallAsync<string>("ProcessData", input);
            var result2 = await context.CallAsync<string>("ValidateData", result1);

            Console.WriteLine($"Demo orchestrator completed with result: {result2}");
            return result2;
        });

        // Register activity functions
        runtime.RegisterJsonFunction("ProcessData", async (context, input) =>
        {
            Console.WriteLine($"Processing data: {input}");
            await Task.Delay(100); // Simulate work
            return $"{{\"processed\": {input}}}";
        });

        runtime.RegisterJsonFunction("ValidateData", async (context, input) =>
        {
            Console.WriteLine($"Validating data: {input}");
            await Task.Delay(50); // Simulate work
            return $"{{\"validated\": {input}}}";
        });
    }
}