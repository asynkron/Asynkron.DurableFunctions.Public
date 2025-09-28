using Asynkron.DurableFunctions.Management;

namespace Asynkron.DurableFunctions.AzureAdapter.Core;

/// <summary>
/// Azure-compatible durable orchestration client interface for compatibility with Azure Durable Functions.
/// Maps to the underlying Asynkron.DurableFunctions.Core.IOrchestrationClient.
/// </summary>
public interface IDurableOrchestrationClient
{
    /// <summary>
    /// Starts a new orchestration instance with the specified function name and input.
    /// </summary>
    /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
    /// <param name="input">The JSON serializable input to pass to the orchestrator function.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the instance ID of the started orchestration.</returns>
    Task<string> StartNewAsync(string orchestratorFunctionName, object? input = null);

    /// <summary>
    /// Starts a new orchestration instance with the specified function name, input, and instance ID.
    /// </summary>
    /// <param name="orchestratorFunctionName">The name of the orchestrator function to start.</param>
    /// <param name="instanceId">A unique ID to use for the new orchestration instance.</param>
    /// <param name="input">The JSON serializable input to pass to the orchestrator function.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the instance ID of the started orchestration.</returns>
    Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object? input = null);

    /// <summary>
    /// Gets the status of the specified orchestration instance.
    /// </summary>
    /// <param name="instanceId">The ID of the orchestration instance to query.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the status of the specified orchestration instance.</returns>
    Task<DurableOrchestrationStatus?> GetStatusAsync(string instanceId);

    /// <summary>
    /// Gets the status of the specified orchestration instance.
    /// </summary>
    /// <param name="instanceId">The ID of the orchestration instance to query.</param>
    /// <param name="showHistory">Boolean marker for including execution history in the response.</param>
    /// <param name="showHistoryOutput">Boolean marker for including input and output in the execution history response.</param>
    /// <param name="showInput">Boolean marker for including the input in the response.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the status of the specified orchestration instance.</returns>
    Task<DurableOrchestrationStatus?> GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput);

    /// <summary>
    /// Sends an event notification message to a waiting orchestration instance.
    /// </summary>
    /// <param name="instanceId">The ID of the orchestration instance that will handle the event.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="eventData">The JSON serializable data associated with the event.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RaiseEventAsync(string instanceId, string eventName, object? eventData = null);

    /// <summary>
    /// Forcefully terminates an orchestration instance.
    /// </summary>
    /// <param name="instanceId">The ID of the orchestration instance to terminate.</param>
    /// <param name="reason">The reason for terminating the orchestration instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task TerminateAsync(string instanceId, string reason);

    /// <summary>
    /// Purges orchestration instance state and history for orchestrations older than the specified threshold time.
    /// </summary>
    /// <param name="instanceId">The ID of the orchestration instance to purge.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains information about the number of purged instances.</returns>
    Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(string instanceId);
}

/// <summary>
/// Azure-compatible orchestration status information.
/// </summary>
public class DurableOrchestrationStatus
{
    /// <summary>
    /// Gets the unique ID of the orchestration instance.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the name of the orchestrator function.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the runtime status of the orchestration instance.
    /// </summary>
    public OrchestrationRuntimeStatus RuntimeStatus { get; set; }

    /// <summary>
    /// Gets the input of the orchestration instance.
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Gets the output of the orchestration instance.
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// Gets the time at which the orchestration instance was created.
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// Gets the time at which the orchestration instance was last updated.
    /// </summary>
    public DateTimeOffset LastUpdatedTime { get; set; }

    /// <summary>
    /// Gets or sets the execution history of the orchestration instance.
    /// </summary>
    public IList<HistoryEvent>? History { get; set; }
}

/// <summary>
/// Azure-compatible history event information.
/// </summary>
public class HistoryEvent
{
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Azure-compatible purge history result.
/// </summary>
public class PurgeHistoryResult
{
    /// <summary>
    /// Gets the number of deleted instances.
    /// </summary>
    public int DeletedInstanceCount { get; set; }
}