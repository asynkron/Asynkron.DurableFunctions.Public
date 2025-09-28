using Asynkron.DurableFunctions.Attributes;
using Asynkron.DurableFunctions.Core;
using JetBrains.Annotations;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating how to use CreateTimer functionality in orchestrations.
/// This shows the exact usage pattern mentioned in the problem statement.
/// </summary>
[PublicAPI]
public class TimerOrchestrationExample
{
    /// <summary>
    /// An example orchestrator that demonstrates the CreateTimer functionality.
    /// This matches the example from the problem statement: putting the orchestrator to sleep for 72 hours.
    /// </summary>
    [FunctionName("SleepyOrchestrator")]
    public async Task<string> LongSleepOrchestrator([OrchestrationTrigger] IOrchestrationContext context)
    {
        // Put the orchestrator to sleep for 72 hours (as per problem statement example)
        var dueTime = context.CurrentUtcDateTime.AddHours(72);
        await context.CreateTimer(dueTime);

        // This orchestrator will be rescheduled to execute at the dueTime.
        // When it runs again after 72 hours, it will reach this point and complete successfully.
        // Once completed successfully, the durable state will be automatically deleted.
        return "Woke up after 72 hours!";
    }

    /// <summary>
    /// A shorter example for testing purposes.
    /// </summary>
    [FunctionName("ShortSleepOrchestrator")]
    public async Task<string> ShortSleepOrchestrator([OrchestrationTrigger] IOrchestrationContext context)
    {
        // Put the orchestrator to sleep for 30 seconds
        var dueTime = context.CurrentUtcDateTime.AddSeconds(30);
        await context.CreateTimer(dueTime);

        return "Woke up after 30 seconds!";
    }

    /// <summary>
    /// Example showing orchestrator that completes immediately without timer.
    /// This will be marked as "Done" and state will be deleted immediately.
    [FunctionName("ImmediateOrchestrator")]
    public async Task<string> ImmediateCompletionOrchestrator(
        [OrchestrationTrigger] IOrchestrationContext context)
    {
        // No timer call, orchestrator completes immediately
        await Task.Delay(100); // Simulate some work
        return "Completed immediately!";
    }
}