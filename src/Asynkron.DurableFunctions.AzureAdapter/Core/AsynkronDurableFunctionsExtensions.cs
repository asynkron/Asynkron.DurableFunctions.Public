using System.Reflection;
using Asynkron.DurableFunctions.AzureAdapter.Attributes;
using Asynkron.DurableFunctions.Core;

namespace Asynkron.DurableFunctions.AzureAdapter.Core;

/// <summary>
/// Extension methods to help bridge Azure Durable Functions APIs with Asynkron.DurableFunctions.
/// </summary>
public static class AsynkronDurableFunctionsExtensions
{
    /// <summary>
    /// Creates an Azure-compatible orchestration context adapter.
    /// </summary>
    /// <param name="context">The Asynkron orchestration context.</param>
    /// <returns>An Azure-compatible orchestration context.</returns>
    public static IDurableOrchestrationContext ToAzureContext(this IOrchestrationContext context)
    {
        return new DurableOrchestrationContextAdapter(context);
    }

    /// <summary>
    /// Creates an Azure-compatible activity context adapter.
    /// </summary>
    /// <param name="context">The Asynkron function context.</param>
    /// <param name="input">The input to the activity function.</param>
    /// <returns>An Azure-compatible activity context.</returns>
    public static IDurableActivityContext ToAzureActivityContext(this IFunctionContext context, string input)
    {
        return new DurableActivityContextAdapter(context, input);
    }

    /// <summary>
    /// Registers an Azure-style orchestrator function with the Asynkron runtime.
    /// This method handles the conversion between Azure attributes and Asynkron registration.
    /// </summary>
    /// <param name="runtime">The Asynkron durable function runtime.</param>
    /// <param name="method">The method representing the orchestrator function.</param>
    /// <param name="instance">The instance containing the method (null for static methods).</param>
    public static void RegisterAzureOrchestrator(this DurableFunctionRuntime runtime, MethodInfo method, object? instance = null)
    {
        var functionNameAttr = method.GetCustomAttribute<FunctionNameAttribute>();
        if (functionNameAttr == null)
        {
            throw new InvalidOperationException($"Method {method.Name} must have a FunctionName attribute");
        }

        var functionName = functionNameAttr.Name;

        // Register the orchestrator with the Asynkron runtime
        runtime.RegisterJsonOrchestrator(functionName, async (context, input) =>
        {
            var azureContext = context.ToAzureContext();
            
            try
            {
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    if (param.GetCustomAttribute<DurableOrchestrationTriggerAttribute>() != null)
                    {
                        args[i] = azureContext;
                    }
                    else
                    {
                        // Handle other parameter types as needed
                        args[i] = null!;
                    }
                }

                var result = method.Invoke(instance, args);
                
                if (result is Task task)
                {
                    await task;
                    
                    // Get result from generic Task<T> if applicable
                    if (task.GetType().IsGenericType)
                    {
                        var resultProperty = task.GetType().GetProperty("Result");
                        return System.Text.Json.JsonSerializer.Serialize(resultProperty?.GetValue(task));
                    }
                    
                    return string.Empty;
                }

                return System.Text.Json.JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error executing Azure orchestrator {functionName}", ex);
            }
        });
    }

    /// <summary>
    /// Registers an Azure-style activity function with the Asynkron runtime.
    /// This method handles the conversion between Azure attributes and Asynkron registration.
    /// </summary>
    /// <param name="runtime">The Asynkron durable function runtime.</param>
    /// <param name="method">The method representing the activity function.</param>
    /// <param name="instance">The instance containing the method (null for static methods).</param>
    public static void RegisterAzureActivity(this DurableFunctionRuntime runtime, MethodInfo method, object? instance = null)
    {
        var functionNameAttr = method.GetCustomAttribute<FunctionNameAttribute>();
        if (functionNameAttr == null)
        {
            throw new InvalidOperationException($"Method {method.Name} must have a FunctionName attribute");
        }

        var functionName = functionNameAttr.Name;

        // Register the activity with the Asynkron runtime
        runtime.RegisterJsonFunction(functionName, async (context, input) =>
        {
            var azureContext = context.ToAzureActivityContext(input);
            
            try
            {
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    if (param.GetCustomAttribute<DurableActivityTriggerAttribute>() != null)
                    {
                        args[i] = azureContext;
                    }
                    else
                    {
                        // Handle direct input parameters
                        if (param.ParameterType == typeof(string))
                        {
                            args[i] = input;
                        }
                        else
                        {
                            args[i] = System.Text.Json.JsonSerializer.Deserialize(input, param.ParameterType) ?? throw new InvalidOperationException($"Failed to deserialize input for parameter {param.Name}");
                        }
                    }
                }

                var result = method.Invoke(instance, args);
                
                if (result is Task task)
                {
                    await task;
                    
                    // Get result from generic Task<T> if applicable
                    if (task.GetType().IsGenericType)
                    {
                        var resultProperty = task.GetType().GetProperty("Result");
                        var taskResult = resultProperty?.GetValue(task);
                        return System.Text.Json.JsonSerializer.Serialize(taskResult);
                    }
                    
                    return string.Empty;
                }

                return System.Text.Json.JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error executing Azure activity {functionName}", ex);
            }
        });
    }

    /// <summary>
    /// Automatically registers all Azure-style functions (orchestrators and activities) from a given type.
    /// </summary>
    /// <param name="runtime">The Asynkron durable function runtime.</param>
    /// <param name="type">The type containing the functions to register.</param>
    /// <param name="instance">The instance of the type (null for static methods).</param>
    public static void RegisterAzureFunctionsFromType(this DurableFunctionRuntime runtime, Type type, object? instance = null)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        
        foreach (var method in methods)
        {
            var functionNameAttr = method.GetCustomAttribute<FunctionNameAttribute>();
            if (functionNameAttr == null) continue;

            // Check if it's an orchestrator (has DurableOrchestrationTrigger parameter)
            var hasOrchestratorTrigger = method.GetParameters()
                .Any(p => p.GetCustomAttribute<DurableOrchestrationTriggerAttribute>() != null);
                
            // Check if it's an activity (has DurableActivityTrigger parameter)
            var hasActivityTrigger = method.GetParameters()
                .Any(p => p.GetCustomAttribute<DurableActivityTriggerAttribute>() != null);

            if (hasOrchestratorTrigger)
            {
                runtime.RegisterAzureOrchestrator(method, instance);
            }
            else if (hasActivityTrigger)
            {
                runtime.RegisterAzureActivity(method, instance);
            }
            else
            {
                // Assume it's an activity if it has FunctionName but no specific triggers
                runtime.RegisterAzureActivity(method, instance);
            }
        }
    }
}