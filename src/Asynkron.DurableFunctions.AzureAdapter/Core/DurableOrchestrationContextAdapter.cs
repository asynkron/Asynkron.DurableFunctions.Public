using Asynkron.DurableFunctions.Core;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.AzureAdapter.Core;

/// <summary>
/// Adapter that bridges Azure's IDurableOrchestrationContext to Asynkron's IOrchestrationContext.
/// This allows Azure Durable Functions code to work with the Asynkron implementation.
/// </summary>
internal sealed class DurableOrchestrationContextAdapter : IDurableOrchestrationContext
{
    private readonly IOrchestrationContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurableOrchestrationContextAdapter"/> class.
    /// </summary>
    /// <param name="context">The underlying Asynkron orchestration context.</param>
    public DurableOrchestrationContextAdapter(IOrchestrationContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public string FunctionName => _context.FunctionName;

    /// <inheritdoc />
    public string? ParentInstanceId => _context.ParentInstanceId;

    /// <inheritdoc />
    public string InstanceId => _context.InstanceId;

    /// <inheritdoc />
    public DateTime CurrentUtcDateTime => _context.CurrentUtcDateTime;

    /// <inheritdoc />
    public async Task<TResult> CallActivityAsync<TResult>(string functionName, object? input = null)
    {
        return await _context.CallAsync<TResult>(functionName, input);
    }

    /// <inheritdoc />
    public async Task CallActivityAsync(string functionName, object? input = null)
    {
        await _context.CallAsync(functionName, input);
    }

    /// <inheritdoc />
    public async Task CreateTimer(DateTime fireAt)
    {
        await _context.CreateTimer(new DateTimeOffset(fireAt));
    }

    /// <inheritdoc />
    public async Task<TResult> WaitForExternalEvent<TResult>(string name)
    {
        return await _context.WaitForEvent<TResult>(name);
    }

    /// <inheritdoc />
    public T GetInput<T>()
    {
        return _context.GetInput<T>();
    }

    /// <inheritdoc />
    public ILogger CreateReplaySafeLogger(string categoryName)
    {
        // In the context of Asynkron.DurableFunctions, we return the context's logger
        // This is replay-safe by design in the underlying implementation
        return _context.GetLogger();
    }

    /// <inheritdoc />
    public async Task<TResult> CallSubOrchestratorAsync<TResult>(string functionName, object? input = null)
    {
        return await _context.CallAsync<TResult>(functionName, input);
    }

    /// <inheritdoc />
    public async Task CallSubOrchestratorAsync(string functionName, object? input = null)
    {
        await _context.CallAsync(functionName, input);
    }
}