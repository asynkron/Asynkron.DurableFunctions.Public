# Getting Started with Asynkron.DurableFunctions

This guide will get you up and running with Asynkron.DurableFunctions in just a few minutes.

## 📋 Prerequisites

- .NET 6.0 or later
- Basic understanding of C# and async/await
- Familiarity with dependency injection (helpful but not required)

## 🚀 Installation

### Package Installation

```bash
dotnet add package Asynkron.DurableFunctions
```

### Optional Packages

```bash
# For SQLite storage (recommended for production)
dotnet add package Asynkron.DurableFunctions.SQLite

# For Azure Functions compatibility
dotnet add package Asynkron.DurableFunctions.AzureAdapter
```

## 🎯 Your First Durable Function

Let's create a simple "Hello World" durable function:

```csharp
using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

// Create the runtime
var stateStore = new InMemoryStateStore();
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var runtime = new DurableFunctionRuntime(
    stateStore,
    loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory);

// Register a simple activity function
runtime.RegisterFunction<string, string>("Greet", async name => 
{
    await Task.Delay(100); // Simulate some work
    return $"Hello, {name}!";
});

// Register an orchestrator function
runtime.RegisterOrchestratorFunction<string, string>("HelloOrchestrator", async context =>
{
    var name = context.GetInput<string>();
    var greeting = await context.CallFunction<string>("Greet", name);
    return $"Orchestrator says: {greeting}";
});

// Trigger the orchestration
await runtime.TriggerAsync("hello-123", "HelloOrchestrator", "World");

// Run the orchestration engine
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await runtime.RunAndPollAsync(cts.Token);
```

## 🏗️ Key Components

### 1. State Store
The state store persists orchestration state:

```csharp
// Development - In-memory (data lost on restart)
var stateStore = new InMemoryStateStore();

// Production - SQLite (persistent)
var stateStore = new SqliteStateStore("Data Source=app.db");
```

### 2. Runtime
The runtime manages orchestration execution:

```csharp
var runtime = new DurableFunctionRuntime(
    stateStore,
    logger,
    loggerFactory: loggerFactory);
```

### 3. Activity Functions
Activity functions perform the actual work:

```csharp
runtime.RegisterFunction<InputType, OutputType>("FunctionName", async input =>
{
    // Your business logic here
    return result;
});
```

**💡 Tip:** Activity functions can also receive an `IFunctionContext` parameter for logging and accessing instance information. See [Activity Functions](activities.md) for more details.

### 4. Orchestrator Functions
Orchestrators coordinate the workflow:

```csharp
runtime.RegisterOrchestratorFunction<InputType, OutputType>("OrchestratorName", async context =>
{
    // Call activity functions
    var result = await context.CallFunction<OutputType>("FunctionName", input);
    return result;
});
```

## 🔄 Multi-Step Workflow Example

Let's build a more realistic example with multiple steps:

```csharp
using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

var stateStore = new InMemoryStateStore();
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var runtime = new DurableFunctionRuntime(stateStore, 
    loggerFactory.CreateLogger<DurableFunctionRuntime>(), 
    loggerFactory: loggerFactory);

// Register activity functions
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

// Register orchestrator
runtime.RegisterOrchestratorFunction<string, string>("DataPipelineOrchestrator", async context =>
{
    var data = context.GetInput<string>();
    
    // Sequential processing
    var validated = await context.CallFunction<string>("ValidateData", data);
    var transformed = await context.CallFunction<string>("TransformData", validated);
    var result = await context.CallFunction<string>("StoreData", transformed);
    
    return $"Pipeline complete: {result}";
});

// Run the workflow
await runtime.TriggerAsync("pipeline-001", "DataPipelineOrchestrator", "sample-data");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await runtime.RunAndPollAsync(cts.Token);
```

## ✅ Key Concepts

### Orchestration Context
The `context` parameter provides access to orchestration capabilities:

```csharp
// Get input data
var input = context.GetInput<MyInputType>();

// Call activity functions
var result = await context.CallFunction<OutputType>("FunctionName", input);

// Create durable timers
await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(5));

// Wait for external events
var approval = await context.WaitForExternalEvent<bool>("ApprovalEvent");
```

### State Persistence
- Orchestrations survive application restarts
- State is automatically checkpointed after each activity
- Failed activities can be retried automatically

### Deterministic Execution
Orchestrator functions must be deterministic:

✅ **Good:**
```csharp
runtime.RegisterOrchestratorFunction<string, string>("GoodOrchestrator", async context =>
{
    // Use context.CurrentUtcDateTime for time
    var dueTime = context.CurrentUtcDateTime.AddMinutes(5);
    await context.CreateTimer(dueTime);
    
    // Call functions for external data
    var data = await context.CallFunction<string>("GetData", "input");
    return data;
});
```

❌ **Bad:**
```csharp
runtime.RegisterOrchestratorFunction<string, string>("BadOrchestrator", async context =>
{
    // Don't use DateTime.Now or DateTime.UtcNow
    var dueTime = DateTime.Now.AddMinutes(5); // ❌ Non-deterministic
    
    // Don't call external services directly
    var httpClient = new HttpClient(); // ❌ Non-deterministic
    var response = await httpClient.GetStringAsync("https://api.example.com");
    
    return response;
});
```

## 🔧 Configuration Options

### Logging
```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Information)
);
```

### Storage Options
```csharp
// In-memory (development)
var stateStore = new InMemoryStateStore();

// SQLite (production)
var stateStore = new SqliteStateStore("Data Source=durable_functions.db");

// Custom implementation
public class MyStateStore : IStateStore
{
    // Implement your storage logic
}
```

## 🎯 What's Next?

Now that you've got the basics:

1. **[Explore Examples](../Examples/README.md)** - See more complex patterns
2. **[Learn Core Concepts](concepts.md)** - Understand the framework deeply
3. **[Study Error Handling](error-handling.md)** - Build resilient workflows
4. **[Set Up Production](deployment.md)** - Deploy to production environments

## 💡 Tips for Success

1. **Start Simple** - Begin with basic workflows, add complexity gradually
2. **Use Logging** - Add comprehensive logging to understand execution flow
3. **Test Locally** - Use in-memory storage for rapid development iteration
4. **Plan for Failures** - Design workflows with error handling in mind
5. **Monitor Performance** - Track execution times and resource usage

## 🆘 Common Issues

### Orchestrator Not Deterministic
**Problem:** Orchestrator behaves inconsistently across replays.
**Solution:** Only use `context` methods, avoid direct external calls.

### State Not Persisting
**Problem:** Orchestrations don't survive restarts.
**Solution:** Use SQLite or custom persistent storage instead of in-memory.

### Functions Not Found
**Problem:** "Function not registered" errors.
**Solution:** Ensure all functions are registered before triggering orchestrations.

Ready to dive deeper? Check out the [Core Concepts](concepts.md) guide!