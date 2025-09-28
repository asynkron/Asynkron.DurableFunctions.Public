# Azure Durable Functions Migration Guide

This guide helps you migrate from Azure Durable Functions to Asynkron.DurableFunctions with minimal code changes.

## 🎯 Why Migrate?

### Break Free from Vendor Lock-in
- **Deploy anywhere**: On-premises, Docker, Kubernetes, any cloud provider
- **No Azure dependencies**: Run without Azure Functions runtime or emulators
- **Cost control**: Predictable infrastructure costs, no per-execution billing
- **Full control**: Own your deployment, scaling, and monitoring

### Enhanced Development Experience
- **Local debugging**: Use standard .NET debugging tools without emulators
- **Faster iteration**: No Azure Functions startup overhead
- **Better testing**: Unit test orchestrations and activities easily
- **Simplified deployment**: Standard .NET application deployment

## 🔄 Migration Overview

The migration process involves three main steps:

1. **Install Asynkron.DurableFunctions** - Replace Azure runtime
2. **Minimal code changes** - Adapt hosting and registration
3. **Deploy anywhere** - Choose your preferred infrastructure

## 📦 Step 1: Package Installation

Replace Azure packages with Asynkron packages:

```bash
# Remove Azure packages
dotnet remove package Microsoft.Azure.WebJobs.Extensions.DurableTask
dotnet remove package Microsoft.Azure.Functions.Worker.Extensions.DurableTask

# Add Asynkron packages
dotnet add package Asynkron.DurableFunctions
dotnet add package Asynkron.DurableFunctions.AzureAdapter  # For compatibility
```

## 🔧 Step 2: Code Migration

### Your Existing Azure Code
This Azure Durable Functions code works with minimal changes:

```csharp
public class OrderProcessingFunctions
{
    [FunctionName("ProcessOrderOrchestrator")]
    public async Task<string> ProcessOrder(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        var order = context.GetInput<OrderRequest>();
        
        // These calls work exactly the same!
        var validated = await context.CallActivityAsync<OrderRequest>("ValidateOrder", order);
        var charged = await context.CallActivityAsync<OrderRequest>("ChargePayment", validated);
        var shipped = await context.CallActivityAsync<OrderRequest>("ShipOrder", charged);
        
        return $"Order {order.Id} processed successfully!";
    }

    [FunctionName("ValidateOrder")]
    public async Task<OrderRequest> ValidateOrder(
        [ActivityTrigger] OrderRequest order)
    {
        // Your existing validation logic
        Console.WriteLine($"Validating order {order.Id}");
        await Task.Delay(100);
        return order;
    }

    [FunctionName("ChargePayment")]
    public async Task<OrderRequest> ChargePayment(
        [ActivityTrigger] OrderRequest order)
    {
        // Your existing payment logic
        Console.WriteLine($"Charging payment for {order.Id}");
        await Task.Delay(200);
        return order;
    }

    [FunctionName("ShipOrder")]
    public async Task<OrderRequest> ShipOrder(
        [ActivityTrigger] OrderRequest order)
    {
        // Your existing shipping logic
        Console.WriteLine($"Shipping order {order.Id}");
        await Task.Delay(150);
        return order;
    }
}
```

### New Hosting Code
Replace Azure Functions hosting with Asynkron runtime:

```csharp
using Asynkron.DurableFunctions;
using Asynkron.DurableFunctions.AzureAdapter;
using Microsoft.Extensions.Logging;

// Create runtime with your preferred storage
var stateStore = new SqliteStateStore("Data Source=durable_functions.db");
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var runtime = new DurableFunctionRuntime(
    stateStore,
    loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory);

// Auto-register your existing functions - no code changes needed!
var functions = new OrderProcessingFunctions();
runtime.RegisterAzureFunctionsFromType(typeof(OrderProcessingFunctions), functions);

// Start the runtime
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Run the orchestration engine
await runtime.RunAndPollAsync(cts.Token);
```

## 🏗️ Migration Patterns

### 1. Basic Function Migration
**Azure Version:**
```csharp
[FunctionName("ProcessData")]
public async Task<string> ProcessData([ActivityTrigger] string data)
{
    // Your business logic
    return $"Processed: {data}";
}
```

**Asynkron Version (Option 1 - Direct):**
```csharp
runtime.RegisterFunction<string, string>("ProcessData", async data =>
{
    // Your business logic (unchanged)
    return $"Processed: {data}";
});
```

**Asynkron Version (Option 2 - Adapter):**
```csharp
// Keep your existing code unchanged, use the adapter
runtime.RegisterAzureFunctionsFromType(typeof(MyFunctions), new MyFunctions());
```

### 2. Orchestrator Migration
**Azure Version:**
```csharp
[FunctionName("DataPipelineOrchestrator")]
public async Task<string> RunPipeline(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var data = context.GetInput<string>();
    
    var validated = await context.CallActivityAsync<string>("ValidateData", data);
    var processed = await context.CallActivityAsync<string>("ProcessData", validated);
    var stored = await context.CallActivityAsync<string>("StoreData", processed);
    
    return stored;
}
```

**Asynkron Version (Direct):**
```csharp
runtime.RegisterOrchestratorFunction<string, string>("DataPipelineOrchestrator", 
    async context =>
    {
        var data = context.GetInput<string>();
        
        // API is nearly identical - just different method names
        var validated = await context.CallFunction<string>("ValidateData", data);
        var processed = await context.CallFunction<string>("ProcessData", validated);
        var stored = await context.CallFunction<string>("StoreData", processed);
        
        return stored;
    });
```

### 3. External Events Migration
**Azure Version:**
```csharp
[FunctionName("ApprovalOrchestrator")]
public async Task<string> WaitForApproval(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var request = context.GetInput<ApprovalRequest>();
    
    await context.CallActivityAsync("SendApprovalRequest", request);
    
    // Wait for external event
    var approved = await context.WaitForExternalEvent<bool>("ApprovalEvent");
    
    return approved ? "Approved" : "Rejected";
}
```

**Asynkron Version:**
```csharp
runtime.RegisterOrchestratorFunction<ApprovalRequest, string>("ApprovalOrchestrator", 
    async context =>
    {
        var request = context.GetInput<ApprovalRequest>();
        
        await context.CallFunction("SendApprovalRequest", request);
        
        // Same API for external events!
        var approved = await context.WaitForExternalEvent<bool>("ApprovalEvent");
        
        return approved ? "Approved" : "Rejected";
    });
```

### 4. Timer Migration
**Azure Version:**
```csharp
[FunctionName("DelayedOrchestrator")]
public async Task<string> DelayedProcess(
    [OrchestrationTrigger] IDurableOrchestrationContext context)
{
    var input = context.GetInput<string>();
    
    // Wait 5 minutes
    var dueTime = context.CurrentUtcDateTime.AddMinutes(5);
    await context.CreateTimer(dueTime, CancellationToken.None);
    
    return await context.CallActivityAsync<string>("ProcessAfterDelay", input);
}
```

**Asynkron Version:**
```csharp
runtime.RegisterOrchestratorFunction<string, string>("DelayedOrchestrator", 
    async context =>
    {
        var input = context.GetInput<string>();
        
        // Same timer API!
        var dueTime = context.CurrentUtcDateTime.AddMinutes(5);
        await context.CreateTimer(dueTime);
        
        return await context.CallFunction<string>("ProcessAfterDelay", input);
    });
```

## 🔄 API Mapping

### Method Name Changes
| Azure Method | Asynkron Method | Notes |
|-------------|----------------|-------|
| `CallActivityAsync<T>()` | `CallFunction<T>()` | Core activity invocation |
| `CallSubOrchestratorAsync<T>()` | `CallSubOrchestratorAsync<T>()` | Same name |
| `WaitForExternalEvent<T>()` | `WaitForExternalEvent<T>()` | Same name |
| `CreateTimer()` | `CreateTimer()` | Same name |
| `GetInput<T>()` | `GetInput<T>()` | Same name |
| `CurrentUtcDateTime` | `CurrentUtcDateTime` | Same name |

### Attribute Changes
| Azure Attribute | Asynkron Registration |
|-----------------|---------------------|
| `[FunctionName]` | `runtime.RegisterFunction()` or Azure adapter |
| `[OrchestrationTrigger]` | `runtime.RegisterOrchestratorFunction()` |
| `[ActivityTrigger]` | `runtime.RegisterFunction()` |

## 🏭 Deployment Options

### Option 1: Console Application
```csharp
// Program.cs
using Asynkron.DurableFunctions;

var stateStore = new SqliteStateStore("Data Source=app.db");
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var runtime = new DurableFunctionRuntime(stateStore, 
    loggerFactory.CreateLogger<DurableFunctionRuntime>(), 
    loggerFactory: loggerFactory);

// Register your functions
runtime.RegisterAzureFunctionsFromAssembly(typeof(Program).Assembly);

// Run
await runtime.RunAndPollAsync(CancellationToken.None);
```

### Option 2: ASP.NET Core Integration
```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Asynkron.DurableFunctions
builder.Services.AddSingleton<IStateStore>(sp => 
    new SqliteStateStore(builder.Configuration.GetConnectionString("StateStore")));
builder.Services.AddSingleton<DurableFunctionRuntime>();

var app = builder.Build();

// Configure your API endpoints
app.MapPost("/orders", async (OrderRequest order, DurableFunctionRuntime runtime) =>
{
    var instanceId = Guid.NewGuid().ToString();
    await runtime.TriggerAsync(instanceId, "ProcessOrderOrchestrator", order);
    return Results.Ok(new { InstanceId = instanceId });
});

// Auto-start the runtime
var runtime = app.Services.GetRequiredService<DurableFunctionRuntime>();
_ = Task.Run(() => runtime.RunAndPollAsync(CancellationToken.None));

app.Run();
```

### Option 3: Background Service
```csharp
public class DurableFunctionService : BackgroundService
{
    private readonly DurableFunctionRuntime _runtime;

    public DurableFunctionService(DurableFunctionRuntime runtime)
    {
        _runtime = runtime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _runtime.RunAndPollAsync(stoppingToken);
    }
}

// In Program.cs
builder.Services.AddHostedService<DurableFunctionService>();
```

## 📊 Storage Migration

### From Azure Storage Tables
**Azure (Task Hub Storage):**
```json
{
  "ConnectionStrings": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=..."
  }
}
```

**Asynkron (SQLite):**
```csharp
var stateStore = new SqliteStateStore("Data Source=durable_functions.db");
```

### Custom Storage Implementation
```csharp
public class CosmosStateStore : IStateStore
{
    // Implement your preferred storage backend
    // - Azure Cosmos DB
    // - PostgreSQL
    // - MongoDB
    // - Redis
    // - Any database you prefer
}
```

## 🧪 Testing Migration

### Unit Testing Activities
**Before (Azure):**
```csharp
[Test]
public async Task TestValidateOrder()
{
    var functions = new OrderProcessingFunctions();
    var order = new OrderRequest { Id = "TEST", Amount = 100 };
    
    // Hard to test without Azure runtime
}
```

**After (Asynkron):**
```csharp
[Test]
public async Task TestValidateOrder()
{
    var functions = new OrderProcessingFunctions();
    var order = new OrderRequest { Id = "TEST", Amount = 100 };
    
    // Easy to test - just call the method!
    var result = await functions.ValidateOrder(order);
    
    Assert.AreEqual("TEST", result.Id);
    Assert.AreEqual(100, result.Amount);
}
```

### Integration Testing
```csharp
[Test]
public async Task TestFullOrchestration()
{
    var stateStore = new InMemoryStateStore();
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var runtime = new DurableFunctionRuntime(stateStore, 
        loggerFactory.CreateLogger<DurableFunctionRuntime>(), 
        loggerFactory: loggerFactory);

    // Register functions
    var functions = new OrderProcessingFunctions();
    runtime.RegisterAzureFunctionsFromType(typeof(OrderProcessingFunctions), functions);

    // Test the full workflow
    var order = new OrderRequest { Id = "TEST", Amount = 100 };
    await runtime.TriggerAsync("test-instance", "ProcessOrderOrchestrator", order);
    
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await runtime.RunAndPollAsync(cts.Token);
    
    // Verify results...
}
```

## ✅ Migration Checklist

### Pre-Migration
- [ ] Review existing Azure Functions code
- [ ] Identify dependencies (storage, external services)
- [ ] Plan storage backend (SQLite, custom)
- [ ] Set up development environment

### Migration Steps
- [ ] Install Asynkron.DurableFunctions packages
- [ ] Remove Azure Functions packages
- [ ] Create new hosting code
- [ ] Register existing functions using adapter
- [ ] Test locally with in-memory storage
- [ ] Configure production storage
- [ ] Update deployment scripts

### Post-Migration
- [ ] Verify all orchestrations work correctly
- [ ] Test error handling and retries
- [ ] Update monitoring and logging
- [ ] Train team on new deployment process
- [ ] Document new architecture

## 🎉 Benefits After Migration

### Development Benefits
- **Faster local development** - No Azure emulator required
- **Better debugging** - Standard .NET debugging experience
- **Easier testing** - Unit test orchestrations directly
- **Simpler CI/CD** - Standard .NET application deployment

### Operational Benefits
- **Deploy anywhere** - On-premises, containers, any cloud
- **Predictable costs** - No per-execution charges
- **Better control** - Own your infrastructure and scaling
- **Simpler monitoring** - Use your preferred tools

### Technical Benefits
- **Faster startup** - No Azure Functions cold start
- **Better performance** - Optimized for your use case
- **Flexible storage** - Choose your preferred database
- **Easier integration** - Standard .NET application patterns

## 🆘 Common Migration Issues

### Issue: Context API Differences
**Problem:** Method names are slightly different.
**Solution:** Use the Azure adapter or update method calls:
- `CallActivityAsync` → `CallFunction`

### Issue: Dependency Injection
**Problem:** Azure Functions DI doesn't map directly.
**Solution:** Use standard .NET DI patterns in your hosting code.

### Issue: Storage Configuration
**Problem:** Azure Storage Tables vs other storage.
**Solution:** Choose appropriate storage backend and update connection strings.

### Issue: Deployment Process
**Problem:** Different deployment model.
**Solution:** Use standard .NET deployment (console app, web app, container).

## 📚 Next Steps

1. **Start with a small function** - Migrate one orchestration first
2. **Test thoroughly** - Verify behavior matches Azure version
3. **Choose storage backend** - SQLite for simplicity, custom for specific needs
4. **Plan deployment** - Choose hosting model (console, web, container)
5. **Monitor and optimize** - Use your preferred monitoring tools

The migration to Asynkron.DurableFunctions gives you the freedom to deploy anywhere while keeping your existing business logic intact!