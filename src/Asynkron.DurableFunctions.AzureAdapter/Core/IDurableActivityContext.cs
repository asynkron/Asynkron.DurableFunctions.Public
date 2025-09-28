using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.AzureAdapter.Core;

/// <summary>
/// Azure-compatible durable activity context interface for compatibility with Azure Durable Functions.
/// Maps to the underlying Asynkron.DurableFunctions.Core.FunctionContext.
/// </summary>
public interface IDurableActivityContext
{
    /// <summary>
    /// Gets the instance ID of the orchestration that called this activity.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// Gets the input to the activity, deserialized to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the input to.</typeparam>
    /// <returns>The deserialized input object.</returns>
    T GetInput<T>();

    /// <summary>
    /// Gets the input to the activity as a string.
    /// </summary>
    /// <returns>The input string.</returns>
    string GetInput();

    /// <summary>
    /// Creates a replay-safe logger for the activity context.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <returns>A logger instance.</returns>
    ILogger CreateReplaySafeLogger(string categoryName);
}