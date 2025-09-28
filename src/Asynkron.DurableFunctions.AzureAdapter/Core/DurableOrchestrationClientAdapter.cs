using Asynkron.DurableFunctions.Core;

namespace Asynkron.DurableFunctions.AzureAdapter.Core;

/// <summary>
/// Adapter that bridges Azure's IDurableOrchestrationClient to Asynkron's IOrchestrationClient.
/// This allows Azure Durable Functions code to work with the Asynkron implementation.
/// </summary>
public sealed class DurableOrchestrationClientAdapter : IDurableOrchestrationClient
{
    private readonly IOrchestrationClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurableOrchestrationClientAdapter"/> class.
    /// </summary>
    /// <param name="client">The underlying Asynkron orchestration client.</param>
    public DurableOrchestrationClientAdapter(IOrchestrationClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task<string> StartNewAsync(string orchestratorFunctionName, object? input = null)
    {
        var result = await _client.StartNewAsync(orchestratorFunctionName, input);
        return result.InstanceId;
    }

    /// <inheritdoc />
    public async Task<string> StartNewAsync(string orchestratorFunctionName, string instanceId, object? input = null)
    {
        var result = await _client.StartNewAsync(orchestratorFunctionName, input, instanceId);
        return result.InstanceId;
    }

    /// <inheritdoc />
    public async Task<DurableOrchestrationStatus?> GetStatusAsync(string instanceId)
    {
        return await GetStatusAsync(instanceId, showHistory: false, showHistoryOutput: false, showInput: true);
    }

    /// <inheritdoc />
    public async Task<DurableOrchestrationStatus?> GetStatusAsync(string instanceId, bool showHistory, bool showHistoryOutput, bool showInput)
    {
        var status = await _client.GetStatusAsync(instanceId, showHistory, showHistoryOutput, showInput);
        if (status == null)
        {
            return null;
        }

        return new DurableOrchestrationStatus
        {
            InstanceId = status.InstanceId,
            Name = status.Name,
            RuntimeStatus = status.RuntimeStatus,
            Input = status.Input,
            Output = status.Output,
            CreatedTime = status.CreatedTime,
            LastUpdatedTime = status.LastUpdatedTime,
            History = showHistory ? new List<HistoryEvent>() : null // TODO: Convert history if needed
        };
    }

    /// <inheritdoc />
    public async Task RaiseEventAsync(string instanceId, string eventName, object? eventData = null)
    {
        await _client.RaiseEventAsync(instanceId, eventName, eventData);
    }

    /// <inheritdoc />
    public async Task TerminateAsync(string instanceId, string reason)
    {
        await _client.TerminateAsync(instanceId, reason);
    }

    /// <inheritdoc />
    public async Task<PurgeHistoryResult> PurgeInstanceHistoryAsync(string instanceId)
    {
        var result = await _client.PurgeInstanceHistoryAsync(instanceId);
        return new PurgeHistoryResult
        {
            DeletedInstanceCount = result.InstancesDeleted
        };
    }
}