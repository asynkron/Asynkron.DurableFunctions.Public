using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

/// <summary>
/// Hello World example - the simplest possible durable function
/// </summary>
public class HelloWorldExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Simple function
        runtime.RegisterFunction<string, string>("Greet", async name => $"Hello {name}! 👋");

        // Simple orchestrator using CallFunction
        runtime.RegisterOrchestratorFunction<string, string>("HelloOrchestrator", async context =>
        {
            var name = context.GetInput<string>();
            return await context.CallFunction<string>("Greet", name);
        });

        // Run it!
        await runtime.TriggerAsync("test", "HelloOrchestrator", "World");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await runtime.RunAndPollAsync(cts.Token);
    }
}