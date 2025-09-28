namespace Asynkron.DurableFunctions.AzureAdapter.Attributes;

/// <summary>
/// Azure-compatible durable activity trigger attribute for compatibility with Azure Durable Functions.
/// Maps to the underlying Asynkron.DurableFunctions.Attributes.FunctionTriggerAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DurableActivityTriggerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableActivityTriggerAttribute"/> class.
    /// </summary>
    public DurableActivityTriggerAttribute()
    {
    }
}