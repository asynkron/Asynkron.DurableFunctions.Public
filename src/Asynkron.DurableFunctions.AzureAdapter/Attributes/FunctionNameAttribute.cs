namespace Asynkron.DurableFunctions.AzureAdapter.Attributes;

/// <summary>
/// Azure-compatible function name attribute for compatibility with Azure Durable Functions.
/// Maps to the underlying Asynkron.DurableFunctions.Attributes.FunctionNameAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FunctionNameAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the function.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionNameAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    public FunctionNameAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}