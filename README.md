### [💬 Join our Slack channel](https://join.slack.com/t/asynkron/shared_invite/zt-ko824601-yGN1d3GHF9jzZX2VtONodQ)

# Asynkron.DurableFunctions

Ultra-fast, distributed, durable functions for .NET applications.

> **Note**: This is a public placeholder repository for Asynkron.DurableFunctions. This repository serves as a central location for bug tracking, documentation, and project information.

## Overview

Asynkron.DurableFunctions provides a powerful framework for building reliable, stateful, long-running workflows in .NET applications. Built on the proven [Proto.Actor](https://github.com/asynkron/protoactor-dotnet) foundation, it combines the actor model's resilience with durable execution patterns.

## Key Features

- **Ultra-fast execution** - Built on Proto.Actor's high-performance actor system
- **Distributed by default** - Scale across multiple nodes seamlessly
- **Fault tolerance** - Automatic recovery from failures with state preservation
- **Durable orchestrations** - Long-running workflows that survive process restarts
- **Activity functions** - Stateless units of work with automatic retry capabilities
- **Entity functions** - Stateful actors with persistent state management
- **Cross-platform** - Runs on .NET 6+ across Windows, Linux, and macOS

## Design Principles

**Minimalistic API** - Simple, intuitive API that's easy to learn and use. No complex enterprise configurations.

**Build on proven technology** - Leverages Proto.Actor's battle-tested actor system and Protobuf serialization.

**Pass data, not objects** - Explicit serialization with Protobuf ensures reliability and performance.

**Be fast** - Designed for high-throughput scenarios without sacrificing reliability.

## Getting Started

### Installation

Using NuGet Package Manager Console:

```
PM> Install-Package Asynkron.DurableFunctions
```

Using .NET CLI:

```bash
dotnet add package Asynkron.DurableFunctions
```

### Hello World Orchestration

Define a simple orchestration:

```csharp
public class HelloWorldOrchestration
{
    [DurableFunction]
    public async Task<string> RunAsync(IDurableOrchestrationContext context)
    {
        var input = context.GetInput<string>();
        var result = await context.CallActivityAsync<string>("SayHello", input);
        return result;
    }
}

public class HelloActivity
{
    [DurableFunction]
    public Task<string> SayHello(string name)
    {
        return Task.FromResult($"Hello, {name}!");
    }
}
```

Start the orchestration:

```csharp
var system = new DurableFunctionSystem();
var client = system.CreateClient();

var instanceId = await client.StartNewAsync("HelloWorldOrchestration", "World");
var result = await client.WaitForCompletionAsync<string>(instanceId);

Console.WriteLine(result); // Output: Hello, World!
```

## Function Types

### Orchestrator Functions

Long-running, reliable workflow functions that coordinate other functions:

```csharp
[DurableFunction]
public async Task<string> ProcessOrderAsync(IDurableOrchestrationContext context)
{
    var order = context.GetInput<Order>();
    
    // Validate order
    await context.CallActivityAsync("ValidateOrder", order);
    
    // Process payment
    var paymentResult = await context.CallActivityAsync<PaymentResult>("ProcessPayment", order.Payment);
    
    // Ship order
    await context.CallActivityAsync("ShipOrder", order);
    
    return "Order processed successfully";
}
```

### Activity Functions

Stateless functions that perform individual units of work:

```csharp
[DurableFunction]
public async Task<bool> ValidateOrderAsync(Order order)
{
    // Validation logic
    return order.Items.Count > 0 && order.Total > 0;
}
```

### Entity Functions

Stateful actors that maintain persistent state:

```csharp
public class CounterEntity : IDurableEntity
{
    public int Value { get; set; }
    
    [DurableFunction]
    public void Add(int amount) => Value += amount;
    
    [DurableFunction]
    public void Reset() => Value = 0;
    
    [DurableFunction]
    public int Get() => Value;
}
```

## Advanced Features

### Sub-Orchestrations

Break complex workflows into manageable sub-orchestrations:

```csharp
[DurableFunction]
public async Task<string> MainOrchestrationAsync(IDurableOrchestrationContext context)
{
    var result1 = await context.CallSubOrchestratorAsync<string>("SubOrchestration1", input1);
    var result2 = await context.CallSubOrchestratorAsync<string>("SubOrchestration2", input2);
    
    return $"Results: {result1}, {result2}";
}
```

### Timers and Delays

Schedule work for the future:

```csharp
[DurableFunction]
public async Task ProcessWithDelayAsync(IDurableOrchestrationContext context)
{
    // Wait for 1 hour
    await context.CreateTimer(DateTime.UtcNow.AddHours(1));
    
    // Continue processing
    await context.CallActivityAsync("ContinueProcessing");
}
```

### External Events

Wait for external signals:

```csharp
[DurableFunction]
public async Task WaitForApprovalAsync(IDurableOrchestrationContext context)
{
    var approvalEvent = await context.WaitForExternalEvent<bool>("ApprovalReceived");
    
    if (approvalEvent)
    {
        await context.CallActivityAsync("ProcessApproval");
    }
}
```

## Configuration

Configure the Durable Functions system:

```csharp
var config = new DurableFunctionConfiguration
{
    TaskHubName = "MyTaskHub",
    StorageProvider = new SqlServerStorageProvider(connectionString),
    MaxConcurrentActivities = 100,
    MaxConcurrentOrchestrations = 50
};

var system = new DurableFunctionSystem(config);
```

## Monitoring and Management

Monitor running orchestrations:

```csharp
var client = system.CreateClient();

// Get orchestration status
var status = await client.GetStatusAsync(instanceId);

// List all running orchestrations
var runningOrchestrations = await client.GetStatusAsync();

// Terminate orchestration
await client.TerminateAsync(instanceId, "User requested termination");
```

## Storage Providers

Multiple storage providers are supported:

- **SQL Server** - High-performance, ACID-compliant storage
- **PostgreSQL** - Cross-platform relational database support
- **In-Memory** - For testing and development scenarios

## Sample Applications

- [E-commerce Order Processing](https://github.com/asynkron/durable-functions-samples/tree/main/ecommerce)
- [IoT Data Processing Pipeline](https://github.com/asynkron/durable-functions-samples/tree/main/iot-pipeline)
- [Human Interaction Workflows](https://github.com/asynkron/durable-functions-samples/tree/main/human-interaction)

## Documentation

Additional documentation is available:

- [Getting Started Guide](https://docs.asynkron.se/durable-functions/getting-started)
- [API Reference](https://docs.asynkron.se/durable-functions/api-reference)
- [Best Practices](https://docs.asynkron.se/durable-functions/best-practices)
- [Migration Guide](https://docs.asynkron.se/durable-functions/migration)

## Community and Support

- 📧 **Email**: [support@asynkron.se](mailto:support@asynkron.se)
- 💬 **Slack**: [Join our Slack workspace](https://join.slack.com/t/asynkron/shared_invite/zt-ko824601-yGN1d3GHF9jzZX2VtONodQ)
- 🐛 **Issues**: [Report bugs and request features](https://github.com/asynkron/Asynkron.DurableFunctions.Public/issues)
- 📖 **Documentation**: [Official documentation](https://docs.asynkron.se/durable-functions)

## Bug Tracking and Feature Requests

This repository serves as the public issue tracker for Asynkron.DurableFunctions:

- **🐛 Bug Reports**: [Create a bug report](https://github.com/asynkron/Asynkron.DurableFunctions.Public/issues/new?template=bug_report.md)
- **✨ Feature Requests**: [Request a new feature](https://github.com/asynkron/Asynkron.DurableFunctions.Public/issues/new?template=feature_request.md)
- **❓ Questions**: [Ask questions and get help](https://github.com/asynkron/Asynkron.DurableFunctions.Public/discussions)

When reporting issues, please include:
- Version information
- Minimal reproduction case
- Expected vs actual behavior
- Environment details (OS, .NET version, etc.)

## Roadmap

- ✅ Core orchestration engine
- ✅ Activity functions
- ✅ Entity functions  
- ✅ SQL Server storage provider
- 🚧 PostgreSQL storage provider
- 🚧 Advanced monitoring and observability
- 📋 Kubernetes deployment templates
- 📋 Visual workflow designer
- 📋 Performance benchmarking tools

## Performance

Asynkron.DurableFunctions is designed for high-performance scenarios:

- **Throughput**: 10,000+ orchestrations per second per node
- **Latency**: Sub-millisecond activity execution
- **Scalability**: Linear scaling across multiple nodes
- **Memory efficiency**: Minimal memory footprint per orchestration

## Contributing

We welcome contributions from the community! Please see our [Contributing Guide](CONTRIBUTING.md) for details on:

- Code of conduct
- Development setup
- Submitting pull requests
- Coding standards

## Partners and Contributors

| Name                                     | Role                                  |
| ---------------------------------------- | ------------------------------------- |
| [Asynkron AB](https://asynkron.se)       | Founder and owner                     |
| Community Contributors                   | Feature development and bug fixes     |

## Related Projects

- **[Proto.Actor](https://github.com/asynkron/protoactor-dotnet)**: The underlying actor system
- **[Wire](https://github.com/asynkron/Wire)**: High-performance .NET serializer
- **[TraceLens](https://github.com/asynkron/TraceLens)**: Distributed tracing and observability

## License

This project is licensed under the [Apache License 2.0](LICENSE).

---

*Built with ❤️ by the Asynkron team and community contributors.*