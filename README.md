# 🚀 Asynkron.DurableFunctions

[![CI/CD Pipeline](https://github.com/asynkron/Asynkron.DurableFunctions/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/asynkron/Asynkron.DurableFunctions/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/Asynkron.DurableFunctions.svg)](https://www.nuget.org/packages/Asynkron.DurableFunctions/)
[![Downloads](https://img.shields.io/nuget/dt/Asynkron.DurableFunctions.svg)](https://www.nuget.org/packages/Asynkron.DurableFunctions/)

> **A powerful durable orchestration framework with our own API design!**

**Asynkron.DurableFunctions** is an independent durable orchestration framework that runs on any .NET environment -
on-premises, Docker, Kubernetes, or any cloud provider. While inspired by the concepts of Azure Durable Functions, this
is **our own project with our own API design**. No vendor lock-in, just pure orchestration power!

## Why Asynkron.DurableFunctions?

* **Independent design** - Our own API, our own ideas, no vendor dependency
* **CallFunction as the core** - Clean, simple function invocation pattern
* **Lightning fast** - No heavyweight runtime overhead
* **Multiple storage backends** - In-memory, SQLite, or bring your own
* **Rich orchestration patterns** - Powerful workflow capabilities
* **Easy debugging** - Debug locally with standard .NET tooling
* **Lightweight** - Minimal dependencies, maximum performance
* **Production ready** - Battle-tested orchestration patterns

## Quick Start

Install the NuGet package:

```bash
dotnet add package Asynkron.DurableFunctions
```

### Your First Durable Function

```csharp
using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

// Create runtime - completely independent!
var stateStore = new InMemoryStateStore();
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var runtime = new DurableFunctionRuntime(
    stateStore,
    loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory);

// Register functions using our CallFunction API
runtime.RegisterFunction<string, string>("SayHello", async name =>
{
    await Task.Delay(100); // Simulate some work
    return $"Hello, {name}!";
});

// Register orchestrator - notice the CallFunction usage!
runtime.RegisterOrchestrator<string>("GreetingOrchestrator", async context =>
{
    var name = context.GetInput<string>();
    var greeting = await context.CallFunction<string>("SayHello", name);
    return $"Orchestrator says: {greeting}";
});

// Trigger and run!
await runtime.TriggerAsyncObject("user123", "GreetingOrchestrator", "World");
await runtime.RunAndPollAsync(CancellationToken.None);
```

**Output:**

```
Orchestrator says: Hello, World!
```

## Feature Support

This library provides comprehensive support for all major durable orchestration patterns:

### External Events

Orchestrations can wait for external events and resume when they arrive. This enables human interaction patterns and
event-driven workflows.

```csharp
// Orchestrator waiting for external event
runtime.RegisterOrchestrator<string>("ApprovalOrchestrator", async context =>
{
    // Send approval request
    await context.CallFunction("SendApprovalRequest", context.GetInput<string>());
    
    // Wait for external approval event
    var approved = await context.WaitForExternalEvent<bool>("ApprovalEvent");
    
    if (approved)
    {
        await context.CallFunction("ProcessApproval", "Approved");
        return "Request approved";
    }
    else
    {
        await context.CallFunction("ProcessRejection", "Rejected");
        return "Request rejected";
    }
});

// Later, raise the event from external system
await runtime.RaiseEventAsync("approval-123", "ApprovalEvent", true);
```

> Each call to `WaitForExternalEvent` reserves its own slot. If an orchestrator waits for the same event multiple times,
> it must receive the same number of `RaiseEventAsync` calls. Events are persisted in FIFO order per name, and the runtime
> logs queue depth when deliveries pile up so you can detect backlogs.

### Sub-orchestrations

Call other orchestrators as sub-orchestrations for complex workflow composition. The runtime automatically handles
parent-child relationships.

```csharp
// Child orchestrator
runtime.RegisterOrchestrator<string>("ProcessOrderOrchestrator", async context =>
{
    var order = context.GetInput<string>();
    await context.CallFunction("ValidateOrder", order);
    await context.CallFunction("ChargePayment", order);
    return "Order processed";
});

// Parent orchestrator calling sub-orchestrator
runtime.RegisterOrchestrator<string>("MainOrchestrator", async context =>
{
    var mainOrder = context.GetInput<string>();
    
    // Call sub-orchestrator explicitly
    var result = await context.CallSubOrchestratorAsync<string>("ProcessOrderOrchestrator", mainOrder);
    
    await context.CallFunction("SendNotification", result);
    return "Workflow completed";
});
```

### Human Interaction

Combine external events with functions to create human approval workflows:

```csharp
runtime.RegisterOrchestrator<string>("HumanApprovalOrchestrator", async context =>
{
    var request = context.GetInput<string>();
    
    // Send approval request to human
    await context.CallFunction("SendApprovalEmail", request);
    
    // Wait for human response (timeout after 24 hours)
    using var cts = new CancellationTokenSource(TimeSpan.FromHours(24));
    try
    {
        var approved = await context.WaitForExternalEvent<bool>("HumanApproval");
        return approved ? "Approved by human" : "Rejected by human";
    }
    catch (OperationCanceledException)
    {
        return "Approval timed out";
    }
});
```

### Eternal Orchestrations

Create long-running monitor patterns using durable timers:

```csharp
runtime.RegisterOrchestrator<string>("MonitorOrchestrator", async context =>
{
    var monitorConfig = context.GetInput<string>();
    
    while (true) // Eternal loop
    {
        // Check system health
        var status = await context.CallFunction<string>("CheckSystemHealth", monitorConfig);
        
        if (status != "OK")
        {
            await context.CallFunction("SendAlert", status);
        }
        
        // Wait 5 minutes before next check
        await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(5));
    }
});
```

## Rich Examples Gallery

### Sequential Function Chain

Our clean CallFunction API in action:

```csharp
[FunctionName("ProcessOrderOrchestrator")]
public async Task<string> ProcessOrder([OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var order = context.GetInput<OrderRequest>();
    
    // Sequential processing - each step waits for the previous
    var validated = await context.CallFunction<OrderRequest>("ValidateOrder", order);
    var charged = await context.CallFunction<OrderRequest>("ChargePayment", validated);  
    var shipped = await context.CallFunction<OrderRequest>("ShipOrder", charged);
    var notified = await context.CallFunction<string>("NotifyCustomer", shipped);
    
    return $"Order {order.Id} processed successfully! {notified}";
}

// Functions are just functions!
[FunctionName("ValidateOrder")]
public async Task<OrderRequest> ValidateOrder([ActivityTrigger] OrderRequest order)
{
    Console.WriteLine($"Validating order {order.Id}...");
    await Task.Delay(500); // Simulate validation
    if (order.Amount <= 0) throw new ArgumentException("Invalid amount");
    return order;
}

[FunctionName("ChargePayment")]  
public async Task<OrderRequest> ChargePayment([ActivityTrigger] OrderRequest order)
{
    Console.WriteLine($"Charging ${order.Amount} for order {order.Id}...");
    await Task.Delay(1000); // Simulate payment processing
    return order;
}
```

### Parallel Functions (Fan-out/Fan-in)

Process multiple things concurrently, then combine results:

```csharp
[FunctionName("ParallelProcessingOrchestrator")]
public async Task<string> ProcessParallel([OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var inputs = new[] { "data1", "data2", "data3", "data4", "data5" };
    
    // Fan-out: Start all functions in parallel
    var tasks = inputs.Select(input => 
        context.CallFunction<string>("ProcessData", input)
    ).ToArray();
    
    // Fan-in: Wait for all to complete
    var results = await Task.WhenAll(tasks);
    
    return $"Processed {results.Length} items: {string.Join(", ", results)}";
}

[FunctionName("ProcessData")]
public async Task<string> ProcessData([ActivityTrigger] string data)
{
    Console.WriteLine($"Processing {data}...");
    await Task.Delay(Random.Shared.Next(500, 1500)); // Simulate variable work
    return $"Processed-{data}";
}
```

### Durable Timers

Create workflows that wait for hours, days, or weeks:

```csharp
[FunctionName("LongRunningProcess")]
public async Task<string> LongRunningProcess([OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var startTime = context.CurrentUtcDateTime;
    
    // Send welcome email immediately
    await context.CallFunction("SendWelcomeEmail", context.GetInput<string>());
    
    // Wait 24 hours (orchestrator will hibernate and wake up automatically!)
    var tomorrow = startTime.AddHours(24);
    await context.CreateTimer(tomorrow);
    
    // Send follow-up email after 24 hours
    await context.CallFunction("SendFollowUpEmail", context.GetInput<string>());
    
    // Wait a whole week! (Server can restart, no problem!)
    var nextWeek = startTime.AddDays(7);
    await context.CreateTimer(nextWeek);
    
    // Send weekly newsletter
    await context.CallFunction("SendWeeklyNewsletter", context.GetInput<string>());
    
    return "Email sequence completed over 7 days!";
}
```

### Human Approval Workflows

Wait for external events (like user approval):

```csharp
[FunctionName("ApprovalWorkflow")]
public async Task<string> ApprovalWorkflow([OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var request = context.GetInput<ApprovalRequest>();
    
    // Submit for approval
    await context.CallFunction("SendApprovalRequest", request);
    
    // Wait for external approval event (could be hours or days!)
    var approvalResult = await context.WaitForExternalEvent<bool>("ApprovalEvent");
    
    if (approvalResult)
    {
        await context.CallFunction("ProcessApprovedRequest", request);
        return "Request approved and processed!";
    }
    else
    {
        await context.CallFunction("HandleRejection", request);
        return "Request was rejected.";
    }
}

// To trigger approval from external system:
// await runtime.RaiseEventAsync(instanceId, "ApprovalEvent", true);
```

### Retry and Error Handling

Built-in resilience patterns:

```csharp
[FunctionName("ResilientOrchestrator")]
public async Task<string> ResilientProcess([OrchestrationTrigger] IDurableOrchestrationContext context)
{
    try
    {
        // This might fail, but will retry automatically
        var result = await context.CallFunction<string>("UnreliableFunction", "test-data");
        return $"Success: {result}";
    }
    catch (Exception ex)
    {
        // Handle failure after all retries exhausted
        await context.CallFunction("LogError", ex.Message);
        return "Failed after retries";
    }
}

[FunctionName("UnreliableFunction")]
public async Task<string> UnreliableFunction([ActivityTrigger] string data)
{
    // Simulate 70% failure rate
    if (Random.Shared.NextDouble() < 0.7)
    {
        throw new InvalidOperationException("Simulated failure!");
    }
    
    return $"Successfully processed: {data}";
}
```

## Advanced Patterns

### Auto-Registration with Reflection

Use familiar function attributes:

```csharp
// Standard function registration patterns
public class MyOrchestrations
{
    [Function("EmailCampaignOrchestrator")]
    public async Task<string> EmailCampaign([OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        var campaign = context.GetInput<Campaign>();
        
        foreach (var customer in campaign.Customers)
        {
            await context.CallFunction("SendPersonalizedEmail", customer);
        }
        
        return $"Sent {campaign.Customers.Count} emails!";
    }

    [Function("SendPersonalizedEmail")]
    public async Task SendPersonalizedEmail([ActivityTrigger] Customer customer)
    {
        // Your email logic here
        Console.WriteLine($"Sending email to {customer.Email}");
        await Task.Delay(100);
    }
}

// Auto-register all functions using reflection
runtime.ScanAndRegister(typeof(MyOrchestrations).Assembly);
```

### Persistent Storage with SQLite

Never lose state, even if your server restarts:

```csharp
// Use SQLite for persistence (survives restarts!)
var connectionString = "Data Source=durable_functions.db";
using var stateStore = new SqliteStateStore(connectionString);
var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

// Your orchestrations will survive server restarts! 🎉
```

### Strongly Typed Orchestrators

Type-safe inputs and outputs:

```csharp
public class OrderRequest
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class OrderResult  
{
    public string OrderId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime ProcessedAt { get; set; }
}

// Strongly typed orchestrator
runtime.RegisterOrchestratorFunction<OrderRequest, OrderResult>("ProcessTypedOrder", async context =>
{
    var order = context.GetInput<OrderRequest>(); // ✅ Type-safe!
    // Grab a replay-safe logger (use GetLogger<MyCategory>() for typed categories)
    var logger = context.GetLogger();
    
    logger.LogInformation($"Processing order for {order.ProductName}");
    
    return new OrderResult
    {
        OrderId = Guid.NewGuid().ToString(),
        Status = "Completed",
        ProcessedAt = DateTime.UtcNow
    };
});
```

## Our Independent Approach

**Asynkron.DurableFunctions** is inspired by orchestration concepts from various sources, but this is **our own
independent project** with **our own API design**.

### Key Principles:

- **CallFunction is the core** - Simple, clean function invocation
- **No vendor dependency** - Runs anywhere .NET runs
- **Our design decisions** - API designed for clarity and power
- **Community-driven** - Open to ideas and contributions

While the orchestration patterns are similar to other durable function frameworks, the API and implementation are
completely independent.

## Use Cases

### Business Processes

- Order processing workflows
- Approval chains
- Document processing pipelines
- Customer onboarding flows

### Data Processing

- ETL pipelines with error handling
- Batch processing with fan-out/fan-in
- Multi-step data transformations
- Report generation workflows

### Integration Scenarios

- Multi-system integration workflows
- API orchestration and aggregation
- Event-driven processing chains
- Saga pattern implementations

### Time-Based Workflows

- Scheduled report generation
- Reminder and notification systems
- Delayed processing workflows
- Long-running business processes

## Getting Started Examples

### 1. Hello World (60 seconds)

```csharp
using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

var stateStore = new InMemoryStateStore();
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var runtime = new DurableFunctionRuntime(
    stateStore,
    loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory);

// Simple function
runtime.RegisterFunction<string, string>("Greet", async name => $"Hello {name}! 👋");

// Simple orchestrator using CallFunction
runtime.RegisterOrchestratorFunction<string, string>("HelloOrchestrator", async context =>
{
    var name = context.GetInput<string>();
    return await context.CallFunction<string>("Greet", name);
});

// Run it!
await runtime.TriggerAsync("test", "HelloOrchestrator", "World");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await runtime.RunAndPollAsync(cts.Token);
```

### 2. Multi-Step Workflow

```csharp
// Register a complete workflow using CallFunction
runtime.RegisterOrchestratorFunction<string, string>("DataPipelineOrchestrator", async context =>
{
    var data = context.GetInput<string>();
    
    // Step 1: Validate
    var validated = await context.CallFunction<string>("ValidateData", data);
    
    // Step 2: Transform  
    var transformed = await context.CallFunction<string>("TransformData", validated);
    
    // Step 3: Store
    var result = await context.CallFunction<string>("StoreData", transformed);
    
    return $"Pipeline complete: {result}";
});

// Register functions
runtime.RegisterFunction<string, string>("ValidateData", async data => 
{
    Console.WriteLine($"🔍 Validating: {data}");
    await Task.Delay(100);
    return $"validated-{data}";
});

runtime.RegisterFunction<string, string>("TransformData", async data =>
{
    Console.WriteLine($"🔄 Transforming: {data}");  
    await Task.Delay(200);
    return $"transformed-{data}";
});

runtime.RegisterFunction<string, string>("StoreData", async data =>
{
    Console.WriteLine($"💾 Storing: {data}");
    await Task.Delay(150);
    return $"stored-{data}";
});
```

### 3. Timer Example

```csharp
runtime.RegisterOrchestratorFunction<string, string>("DelayedGreeting", async context =>
{
    var name = context.GetInput<string>();
    
    Console.WriteLine($"⏰ Setting timer for 5 seconds...");
    var dueTime = context.CurrentUtcDateTime.AddSeconds(5);
    await context.CreateTimer(dueTime);
    
    Console.WriteLine($"🎉 Timer fired! Greeting {name}");
    return $"Hello {name} (after delay)!";
});
```

## Configuration Options

### Storage Backends

#### In-Memory (Development)

```csharp  
var stateStore = new InMemoryStateStore();
```

#### SQLite (Production)

```csharp
var stateStore = new SqliteStateStore("Data Source=app.db");
```

#### Custom Storage

```csharp
public class MyCustomStateStore : IStateStore
{
    // Implement your storage logic (Redis, MongoDB, etc.)
}
```

### Logging Integration

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddConsole()
        .AddSerilog() // Or any logging provider
        .SetMinimumLevel(LogLevel.Information)
);
```

### ASP.NET Core Integration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add durable functions as a service
builder.Services.AddSingleton<IStateStore>(sp => 
    new SqliteStateStore(builder.Configuration.GetConnectionString("StateStore")));
    
builder.Services.AddSingleton<DurableFunctionRuntime>();

var app = builder.Build();

// Auto-start the runtime
var runtime = app.Services.GetRequiredService<DurableFunctionRuntime>();
_ = Task.Run(() => runtime.RunAndPollAsync(CancellationToken.None));

app.Run();
```

## Performance & Scalability

* **Lightweight**: Minimal overhead compared to Azure Functions runtime
* **Fast startup**: No cold start issues
* **Horizontally scalable**: Run multiple instances with shared storage
* **Efficient storage**: Optimized state serialization
* **Automatic cleanup**: Completed orchestrations are automatically cleaned up

## Community & Support

* **Documentation**: Comprehensive guides and examples

- 💬 **Community**: Active discussions and support

* **Issues**: Report bugs and request features on GitHub
* **Samples**: Rich sample repository with real-world scenarios

## What People Are Saying

> *"Finally! I can use durable functions in my on-premises applications without Azure!"* - Happy Developer

> *"The migration from Azure Durable Functions was seamless. Same API, better control!"* - DevOps Engineer

> *"Perfect for Kubernetes deployments. No vendor lock-in!"* - Cloud Architect

## Development & Testing

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Running Examples

```bash
cd examples
dotnet run
```

### Creating NuGet Package

```bash
dotnet pack src/Asynkron.DurableFunctions/Asynkron.DurableFunctions.csproj --configuration Release --output ./artifacts
```

---

<div align="center">

**🎉 Ready to break free from Azure lock-in?**

**⭐ Star this repo** • **🍴 Fork it** • **📦 Use it in production**

**[Get Started Now](#-quick-start)** • **[View Examples](#-rich-examples-gallery)** • *
*[Migration Guide](#-azure-functions-migration-guide)**

---

*Built with ❤️ by the Asynkron team*

</div>
