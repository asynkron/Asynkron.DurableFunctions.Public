using Asynkron.DurableFunctions.Attributes;
using Asynkron.DurableFunctions.Core;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example orchestrator showing how to use the Asynkron Durable Functions attributes and interfaces.
/// This demonstrates the same pattern as Azure Durable Functions but with "Asynkron" prefixes.
/// </summary>
public class SampleOrchestrator
{
    /// <summary>
    /// An example orchestrator function that calls multiple activities in sequence.
    /// This is similar to how Azure Durable Functions orchestrators work.
    /// </summary>
    /// <param name="context">The durable orchestration context.</param>
    /// <returns>A task representing the orchestration workflow.</returns>
    [FunctionName("SampleOrchestrator")]
    public async Task<string> RunOrchestrator([OrchestrationTrigger] IOrchestrationContext context)
    {
        // In a real implementation, these would actually invoke activity functions
        // For now, these will throw NotImplementedException as they're stubs

        // Call the first activity
        var result1 = await context.CallAsync<string>("SayHello", "Tokyo");

        // Call the second activity
        var result2 = await context.CallAsync<string>("SayHello", "Seattle");

        // Call the third activity
        var result3 = await context.CallAsync<string>("SayHello", "London");

        // Return the combined results
        return $"{result1}, {result2}, {result3}";
    }

    /// <summary>
    /// An example activity function that would be called by the orchestrator.
    /// </summary>
    /// <param name="name">The name to greet.</param>
    /// <returns>A greeting message.</returns>
    [FunctionName("SayHello")]
    public string SayHello(string name)
    {
        return $"Hello {name}!";
    }

    /// <summary>
    /// An example of an orchestrator that waits for external events.
    /// </summary>
    /// <param name="context">The durable orchestration context.</param>
    /// <returns>A task representing the orchestration workflow.</returns>
    [FunctionName("WaitForEventOrchestrator")]
    public async Task<string> WaitForEventOrchestrator([OrchestrationTrigger] IOrchestrationContext context)
    {
        // Wait for an external event (this would be a stub in the current implementation)
        var eventData = await context.WaitForEvent<string>("UserInput");

        // Process the event data
        var result = await context.CallAsync<string>("ProcessUserInput", eventData);

        return result;
    }
}