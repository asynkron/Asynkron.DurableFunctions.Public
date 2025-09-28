using System.Text.Json;
using Asynkron.DurableFunctions.Core;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.AzureAdapter.Core;

/// <summary>
/// Adapter that bridges Azure's IDurableActivityContext to Asynkron's IFunctionContext.
/// This allows Azure Durable Functions code to work with the Asynkron implementation.
/// </summary>
internal sealed class DurableActivityContextAdapter : IDurableActivityContext
{
    private readonly IFunctionContext _context;
    private readonly string _input;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurableActivityContextAdapter"/> class.
    /// </summary>
    /// <param name="context">The underlying Asynkron function context.</param>
    /// <param name="input">The input to the activity function.</param>
    public DurableActivityContextAdapter(IFunctionContext context, string input)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    /// <inheritdoc />
    public string InstanceId => _context.InstanceId;

    /// <inheritdoc />
    public T GetInput<T>()
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)_input;
        }

        return JsonSerializer.Deserialize<T>(_input) ?? throw new InvalidOperationException($"Failed to deserialize input to type {typeof(T).Name}");
    }

    /// <inheritdoc />
    public string GetInput()
    {
        return _input;
    }

    /// <inheritdoc />
    public ILogger CreateReplaySafeLogger(string categoryName)
    {
        // In the context of Asynkron.DurableFunctions, we return a simple logger
        // Activities are stateless, so replay safety is not a concern here
        return _context.GetLogger();
    }
}