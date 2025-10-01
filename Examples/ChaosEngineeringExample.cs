using System;
using System.Threading.Tasks;
using Asynkron.DurableFunctions.Chaos;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Demonstrates chaos engineering capabilities in Durable Functions.
/// This example shows how to inject failures and latency to test resilience.
/// </summary>
public static class ChaosEngineeringExample
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Chaos Engineering Example ===\n");

        // Create a chaos-enabled configuration (disabled by default for safety)
        var chaosConfig = new ChaosConfiguration
        {
            Enabled = true,
            Environment = "Development", // Safe for non-production
            DefaultLatencyProbability = 0.3, // 30% chance of latency injection
            DefaultFailureProbability = 0.1, // 10% chance of failure injection
            DefaultLatencyDelay = TimeSpan.FromMilliseconds(200)
        };

        // Setup the runtime
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();

        var basicChaosAgent = new BasicChaosAgent(loggerFactory.CreateLogger<BasicChaosAgent>());
        var options = new DurableFunctionRuntimeOptions()
            .UseChaos(basicChaosAgent, chaosConfig, loggerFactory);

        var runtime = new DurableFunctionRuntime(stateStore, logger, options, loggerFactory);

        // Register functions that might fail
        runtime.RegisterJsonFunction("ProcessPayment", async (context, input) =>
        {
            var logger = context.GetLogger();
            logger.LogInformation("Processing payment: {Input} for instance {InstanceId}", input, context.InstanceId);
            Console.WriteLine($"💳 Processing payment: {input}");
            await Task.Delay(100); // Simulate processing time
            return $"Payment processed: {input}";
        });

        runtime.RegisterJsonFunction("SendNotification", async (context, input) =>
        {
            var logger = context.GetLogger();
            logger.LogInformation("Sending notification: {Input} for instance {InstanceId}", input, context.InstanceId);
            Console.WriteLine($"📧 Sending notification: {input}");
            await Task.Delay(50); // Simulate network delay
            return $"Notification sent: {input}";
        });

        runtime.RegisterJsonFunction("UpdateInventory", async (context, input) =>
        {
            var logger = context.GetLogger();
            logger.LogInformation("Updating inventory: {Input} for instance {InstanceId}", input, context.InstanceId);
            Console.WriteLine($"📦 Updating inventory: {input}");
            await Task.Delay(80); // Simulate database operation
            return $"Inventory updated: {input}";
        });

        // Register the chaos orchestrator that demonstrates resilience testing
        runtime.RegisterJsonOrchestrator("ChaosOrderWorkflow", async (context, input) =>
        {
            Console.WriteLine("🎯 Starting chaos-enabled order workflow...");

            try
            {
                // These calls will have chaos injected (latency and potential failures)
                Console.WriteLine("🔧 Step 1: Processing payment with chaos injection...");
                var paymentResult = await context.CallAsync<string>("ProcessPayment", input);
                Console.WriteLine($"✅ Payment result: {paymentResult}");

                Console.WriteLine("🔧 Step 2: Sending notification with chaos injection...");
                var notificationResult = await context.CallAsync<string>("SendNotification", paymentResult);
                Console.WriteLine($"✅ Notification result: {notificationResult}");

                Console.WriteLine("🔧 Step 3: Updating inventory with chaos injection...");
                var inventoryResult = await context.CallAsync<string>("UpdateInventory", paymentResult);
                Console.WriteLine($"✅ Inventory result: {inventoryResult}");

                return $"Order completed: {inventoryResult}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Chaos-induced failure in workflow: {ex.Message}");
                
                // In a real system, you would implement compensation logic here
                Console.WriteLine("🔄 Implementing compensation logic...");
                return $"Order failed but compensated: {ex.Message}";
            }
        });

        // Run multiple instances to demonstrate chaos behavior
        Console.WriteLine("🚀 Running multiple order workflows to demonstrate chaos...\n");
        
        for (int i = 1; i <= 5; i++)
        {
            var instanceId = $"chaos-order-{i}";
            try
            {
                await runtime.TriggerAsync(instanceId, "ChaosOrderWorkflow", $"Order-{i:D3}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Chaos prevented scheduling for {instanceId}: {ex.Message}");
            }
        }

        // Run the runtime to execute the workflows
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n⏰ Chaos example completed (timeout reached)");
        }

        Console.WriteLine("\n=== Chaos Engineering Summary ===");
        Console.WriteLine("This example demonstrated:");
        Console.WriteLine("• Chaos injection into orchestration function calls");
        Console.WriteLine("• Production safety mechanisms (environment checks)");
        Console.WriteLine("• Configurable failure and latency probabilities");
        Console.WriteLine("• Error handling and compensation patterns");
        Console.WriteLine("• Integration with existing orchestration framework");
    }

    /// <summary>
    /// Example of resource exhaustion testing.
    /// </summary>
    public static async Task DemonstrateResourceChaos()
    {
        Console.WriteLine("=== Resource Chaos Example ===\n");

        var chaosAgent = new BasicChaosAgent();

        Console.WriteLine("🧠 Testing memory pressure simulation...");
        await chaosAgent.InjectResourceExhaustionAsync(ResourceType.Memory, TimeSpan.FromSeconds(2));
        Console.WriteLine("✅ Memory pressure test completed");

        Console.WriteLine("⚡ Testing CPU stress simulation...");
        await chaosAgent.InjectResourceExhaustionAsync(ResourceType.Cpu, TimeSpan.FromSeconds(1));
        Console.WriteLine("✅ CPU stress test completed");

        Console.WriteLine("🌐 Testing network partition simulation...");
        await chaosAgent.InjectNetworkPartitionAsync(TimeSpan.FromSeconds(1));
        Console.WriteLine("✅ Network partition test completed");
    }
}
