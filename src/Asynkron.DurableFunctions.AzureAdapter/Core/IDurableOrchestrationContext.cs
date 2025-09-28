using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.AzureAdapter.Core;

/// <summary>
/// Azure-compatible durable orchestration context interface for compatibility with Azure Durable Functions.
/// Maps to the underlying Asynkron.DurableFunctions.Core.IOrchestrationContext.
/// </summary>
public interface IDurableOrchestrationContext
{
    /// <summary>
    /// Gets the name of the function associated with the current orchestration context.
    /// </summary>
    string FunctionName { get; }

    /// <summary>
    /// Gets the instance ID of the parent orchestration, if this orchestration was started by another orchestration.
    /// </summary>
    string? ParentInstanceId { get; }

    /// <summary>
    /// Gets the instance ID of the orchestration.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// Gets the current date and time in UTC.
    /// </summary>
    DateTime CurrentUtcDateTime { get; }

    /// <summary>
    /// Calls an activity function asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The return type of the activity function.</typeparam>
    /// <param name="functionName">The name of the activity function to call.</param>
    /// <param name="input">The JSON serializable input to pass to the activity function.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the output of the activity function.</returns>
    Task<TResult> CallActivityAsync<TResult>(string functionName, object? input = null);

    /// <summary>
    /// Calls an activity function asynchronously without expecting a return value.
    /// </summary>
    /// <param name="functionName">The name of the activity function to call.</param>
    /// <param name="input">The input to pass to the activity function.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CallActivityAsync(string functionName, object? input = null);

    /// <summary>
    /// Creates a timer that expires at the specified time.
    /// </summary>
    /// <param name="fireAt">The time when the timer should fire.</param>
    /// <returns>A task that completes when the timer expires.</returns>
    Task CreateTimer(DateTime fireAt);

    /// <summary>
    /// Waits for an external event to be raised.
    /// </summary>
    /// <typeparam name="TResult">The type of the event data.</typeparam>
    /// <param name="name">The name of the event to wait for.</param>
    /// <returns>A task that completes when the event is raised. The task result contains the event data.</returns>
    Task<TResult> WaitForExternalEvent<TResult>(string name);

    /// <summary>
    /// Gets the input to the orchestration, deserialized to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the input to.</typeparam>
    /// <returns>The deserialized input object.</returns>
    T GetInput<T>();

    /// <summary>
    /// Gets a logger for the orchestration context.
    /// </summary>
    /// <returns>A logger instance.</returns>
    ILogger CreateReplaySafeLogger(string categoryName);

    /// <summary>
    /// Calls a sub-orchestrator function asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The return type of the sub-orchestrator function.</typeparam>
    /// <param name="functionName">The name of the sub-orchestrator function to call.</param>
    /// <param name="input">The JSON serializable input to pass to the sub-orchestrator function.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the output of the sub-orchestrator function.</returns>
    Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, object? input = null);

    /// <summary>
    /// Calls a sub-orchestrator function asynchronously without expecting a return value.
    /// </summary>
    /// <param name="functionName">The name of the sub-orchestrator function to call.</param>
    /// <param name="input">The input to pass to the sub-orchestrator function.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CallSubOrchestratorAsync(string functionName, object? input = null);
}