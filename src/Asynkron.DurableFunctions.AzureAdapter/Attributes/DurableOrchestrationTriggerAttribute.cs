namespace Asynkron.DurableFunctions.AzureAdapter.Attributes;

/// <summary>
/// Azure-compatible durable orchestration trigger attribute for compatibility with Azure Durable Functions.
/// Maps to the underlying Asynkron.DurableFunctions.Attributes.OrchestrationTriggerAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DurableOrchestrationTriggerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DurableOrchestrationTriggerAttribute"/> class.
    /// </summary>
    public DurableOrchestrationTriggerAttribute()
    {
    }
}