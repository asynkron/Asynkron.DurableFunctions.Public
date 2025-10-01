using System.Text.Json;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Demonstrates the basic functionality of the Durable Function runtime.
/// </summary>
public class DurableFunctionDemo
{
    /// <summary>
    /// Runs a demonstration of the durable function system.
    /// </summary>
    public static async Task RunDemoAsync()
    {
        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();

        // Create state store and runtime
        var stateStore = new InMemoryStateStore();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        Console.WriteLine("=== Durable Function Runtime Demo ===");
        Console.WriteLine();

        // Register some example functions
        runtime.RegisterJsonFunction("SayHello", (context, input) =>
        {
            var logger = context.GetLogger();
            var name = input.ToString();
            logger.LogInformation("Activity SayHello executing for {Name}, instance {InstanceId}", name, context.InstanceId);
            Console.WriteLine($"Hello, {name}!");
            return Task.FromResult<string>($"Greeted {name}");
        });

        runtime.RegisterJsonFunction("CalculateSum", (context, input) =>
        {
            var logger = context.GetLogger();
            logger.LogInformation("Activity CalculateSum executing for instance {InstanceId}", context.InstanceId);
            var i = JsonSerializer.Deserialize<object>(input);
            if (i is JsonElement jsonElement)
            {
                var a = jsonElement.GetProperty("a").GetInt32();
                var b = jsonElement.GetProperty("b").GetInt32();
                var result = a + b;
                Console.WriteLine($"Calculated: {a} + {b} = {result}");
                var r = JsonSerializer.Serialize<object>(result);
                return Task.FromResult(r);
            }

            return Task.FromResult<string>("0");
        });

        // Trigger some functions
        Console.WriteLine("Triggering functions...");
        await runtime.TriggerAsync("abc", "SayHello", "Alice", DateTimeOffset.UtcNow.AddSeconds(2));
        await runtime.TriggerAsync("abc", "SayHello", "Bob", DateTimeOffset.UtcNow.AddSeconds(4));
        await runtime.TriggerAsyncObject("abc", "CalculateSum", new { a = 10, b = 20 },
            DateTimeOffset.UtcNow.AddSeconds(6));

        Console.WriteLine($"Current states in store: {stateStore.Count}");
        Console.WriteLine();

        // Run the polling loop for 10 seconds
        Console.WriteLine("Starting polling loop for 10 seconds...");
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Polling loop stopped.");
        }

        Console.WriteLine();
        Console.WriteLine($"Final states in store: {stateStore.Count}");
        Console.WriteLine("Demo completed.");
    }
}