# Asynkron.DurableFunctions.AzureAdapter

This package provides Azure Durable Functions compatibility shims for `Asynkron.DurableFunctions`, allowing for an almost drop-in replacement of Azure Durable Functions with the Asynkron implementation.

## What it provides

- **Azure-compatible attributes**: `FunctionName`, `DurableOrchestrationTrigger`, `DurableActivityTrigger`
- **Azure-compatible interfaces**: `IDurableOrchestrationContext`, `IDurableActivityContext`
- **Adapter classes**: Bridge Azure APIs to Asynkron implementations
- **Extension methods**: Easy registration of Azure-style functions with Asynkron runtime

## Usage

### 1. Install the packages

```bash
dotnet add package Asynkron.DurableFunctions.AzureAdapter
```

### 2. Use Azure-compatible attributes and interfaces

```csharp
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

public class MyDurableFunctions
{
    [FunctionName("MyOrchestrator")]
    public async Task<string> RunOrchestrator([DurableOrchestrationTrigger] IDurableOrchestrationContext context)
    {
        var result1 = await context.CallActivityAsync<string>("SayHello", "Tokyo");
        var result2 = await context.CallActivityAsync<string>("SayHello", "Seattle");
        var result3 = await context.CallActivityAsync<string>("SayHello", "London");
        
        return $"{result1}, {result2}, {result3}";
    }

    [FunctionName("SayHello")]
    public string SayHello([DurableActivityTrigger] IDurableActivityContext context)
    {
        var name = context.GetInput<string>();
        return $"Hello {name}!";
    }
}
```

### 3. Register with Asynkron runtime

```csharp
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

// Setup the runtime
var stateStore = new InMemoryStateStore();
var runtime = new DurableFunctionRuntime(stateStore);

// Register all Azure-style functions from your class
var functions = new MyDurableFunctions();
runtime.RegisterAzureFunctionsFromType(typeof(MyDurableFunctions), functions);

// Start the runtime
await runtime.RunAndPollAsync(cancellationToken);
```

## Key Differences from Azure Durable Functions

1. **State Store**: Uses Asynkron's state store implementations (InMemory, SQLite) instead of Azure Storage
2. **Hosting**: Runs in any .NET environment, not just Azure Functions
3. **Logging**: Uses Microsoft.Extensions.Logging instead of Azure Functions logging
4. **Deployment**: Can be deployed anywhere (Docker, Kubernetes, on-premises, any cloud)

## Benefits

- **Vendor Independence**: Not locked into Azure infrastructure
- **Local Development**: Full functionality without Azure dependencies
- **Cost Control**: No per-execution billing like Azure Functions
- **Flexibility**: Deploy and scale on any platform
- **Migration Path**: Easy migration from Azure Durable Functions

## Limitations

The following Azure Durable Functions features are not available or have different implementations:

**Azure-specific Infrastructure Features:**
- Azure Storage (Tables, Blobs, Queues) as the underlying storage mechanism
- Azure Functions runtime hosting and scaling model
- Integration with Azure Functions bindings and triggers (HTTP, Service Bus, Event Hub, etc.)
- Azure Functions premium plan features (VNET integration, pre-warmed instances)

**Monitoring and Observability:**
- Built-in Azure Application Insights integration
- Azure Functions monitoring dashboard
- Azure Storage Explorer for state inspection
- Azure Functions diagnostic tools and profiling

**Management and Operations:**
- Azure Functions management APIs and Azure CLI integration
- Azure Resource Manager (ARM) templates for deployment
- Azure Functions deployment slots and staging environments
- Azure Functions authentication and authorization (EasyAuth)

**Advanced Runtime Features:**
- Durable Functions Task Hub management via Azure Storage
- Built-in HTTP APIs for orchestration management (start, query, terminate, etc.)
- Durable Functions extension bundles and version management
- Integration with Azure Functions Core Tools for local development

**Networking and Security:**
- Azure Functions networking features (private endpoints, VNET integration)
- Managed identity integration with Azure services
- Azure Key Vault integration for secrets management
- Azure Functions access keys and function-level security

**Scaling and Performance:**
- Azure Functions consumption plan auto-scaling
- Azure Functions premium plan performance guarantees
- Integration with Azure Load Balancer and Traffic Manager
- Azure Functions concurrency and throughput optimizations specific to Azure infrastructure

**Other Azure Ecosystem Integration:**
- Direct integration with other Azure services through managed connectors
- Azure Logic Apps integration and workflow orchestration
- Azure Event Grid integration for event-driven architectures
- Azure Service Fabric integration for microservices patterns

**Alternative Implementations:**
- Uses local state stores (InMemory, SQLite) instead of Azure Storage
- Uses Microsoft.Extensions.Logging instead of Azure Functions logging
- Different monitoring and debugging integration points
- Portable deployment model instead of Azure Functions hosting

This adapter provides the core functionality needed for most durable function scenarios while maintaining Azure API compatibility.