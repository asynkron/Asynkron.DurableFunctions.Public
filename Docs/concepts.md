# Core Concepts

Understanding these fundamental concepts is key to mastering Asynkron.DurableFunctions.

## 🧠 Fundamental Concepts

### Durable Orchestration
Durable orchestration is a programming model for building reliable, stateful workflows that can:
- **Survive failures** - Continue execution after crashes or restarts
- **Handle long-running processes** - Wait for hours, days, or weeks
- **Coordinate distributed work** - Manage complex multi-step processes
- **Ensure consistency** - Guarantee execution order and state integrity

### The Actor Model Influence
While Asynkron.DurableFunctions is its own framework, it benefits from Asynkron's expertise in the Actor Model:
- **State isolation** - Each orchestration instance maintains its own state
- **Message-driven execution** - Events and function calls drive execution
- **Fault tolerance** - Failures are contained and recoverable

## 🏗️ Core Components

### 1. Orchestrator Functions
Orchestrators are the **coordinators** of your workflow. They:
- Define the workflow logic and execution order
- Call activity functions and sub-orchestrators
- Handle timers, external events, and error conditions
- Must be **deterministic** for replay consistency

```csharp
runtime.RegisterOrchestratorFunction<OrderRequest, string>("ProcessOrder", async context =>
{
    var order = context.GetInput<OrderRequest>();
    
    // Orchestrator coordinates the workflow
    var validated = await context.CallFunction<OrderRequest>("ValidateOrder", order);
    var charged = await context.CallFunction<OrderRequest>("ChargePayment", validated);
    var shipped = await context.CallFunction<OrderRequest>("ShipOrder", charged);
    
    return $"Order {order.Id} completed";
});
```

### 2. Activity Functions
Activity functions perform the **actual work**. They:
- Execute business logic and external operations
- Can have side effects (database writes, API calls, etc.)
- Are automatically retried on failure
- Should be **idempotent** when possible

```csharp
runtime.RegisterFunction<OrderRequest, OrderRequest>("ValidateOrder", async order =>
{
    // Activity function does the actual work
    Console.WriteLine($"Validating order {order.Id}");
    
    // Can make external calls, write to databases, etc.
    await DatabaseService.LogValidation(order.Id);
    
    if (order.Amount <= 0)
        throw new ArgumentException("Invalid order amount");
        
    return order;
});
```

### 3. State Store
The state store provides **durable persistence**:
- Stores orchestration state and execution history
- Enables recovery after failures
- Supports different backends (in-memory, SQLite, custom)

```csharp
// Development - fast but non-persistent
var stateStore = new InMemoryStateStore();

// Production - persistent and reliable
var stateStore = new SqliteStateStore("Data Source=orchestrations.db");
```

### 4. Runtime
The runtime **manages execution**:
- Schedules and executes orchestrations
- Handles state persistence and recovery
- Manages timers and external events
- Provides logging and monitoring hooks

```csharp
var runtime = new DurableFunctionRuntime(
    stateStore,
    logger,
    loggerFactory: loggerFactory);
```

## 🔄 Execution Model

### Deterministic Orchestration
Orchestrators must be **deterministic** - they must produce the same results when replayed:

✅ **Deterministic (Good):**
```csharp
runtime.RegisterOrchestratorFunction<string, string>("GoodOrchestrator", async context =>
{
    // Use context for time
    var now = context.CurrentUtcDateTime;
    var dueTime = now.AddMinutes(5);
    await context.CreateTimer(dueTime);
    
    // Use functions for external data
    var data = await context.CallFunction<string>("GetExternalData", "param");
    
    // Safe to use deterministic operations
    var processedData = data.ToUpperInvariant();
    
    return processedData;
});
```

❌ **Non-deterministic (Bad):**
```csharp
runtime.RegisterOrchestratorFunction<string, string>("BadOrchestrator", async context =>
{
    // Don't use DateTime.Now - non-deterministic on replay
    var now = DateTime.Now; // ❌
    
    // Don't call external services directly
    var httpClient = new HttpClient();
    var data = await httpClient.GetStringAsync("https://api.example.com"); // ❌
    
    // Don't use random numbers directly
    var randomValue = Random.Shared.Next(); // ❌
    
    return data;
});
```

### Replay and Recovery
When an orchestration resumes after a failure:

1. **State is loaded** from the store
2. **History is replayed** to reconstruct the current state
3. **Execution continues** from the last checkpoint
4. **Deterministic behavior** ensures consistency

```csharp
// This orchestration can be safely replayed
runtime.RegisterOrchestratorFunction<string, string>("ReplayableOrchestrator", async context =>
{
    var input = context.GetInput<string>();
    
    // Step 1: This completed before failure - will be skipped on replay
    var step1 = await context.CallFunction<string>("Step1", input);
    
    // Step 2: This failed - will be retried on replay
    var step2 = await context.CallFunction<string>("Step2", step1);
    
    // Step 3: This hasn't run yet - will execute after step 2 succeeds
    var step3 = await context.CallFunction<string>("Step3", step2);
    
    return step3;
});
```

## 🎭 Orchestration Patterns

### Sequential Processing
Execute activities one after another:

```csharp
runtime.RegisterOrchestratorFunction<string, string>("SequentialPattern", async context =>
{
    var input = context.GetInput<string>();
    
    var result1 = await context.CallFunction<string>("Step1", input);
    var result2 = await context.CallFunction<string>("Step2", result1);
    var result3 = await context.CallFunction<string>("Step3", result2);
    
    return result3;
});
```

### Parallel Processing (Fan-out/Fan-in)
Execute activities concurrently, then combine results:

```csharp
runtime.RegisterOrchestratorFunction<string[], string>("ParallelPattern", async context =>
{
    var inputs = context.GetInput<string[]>();
    
    // Fan-out: Start all activities in parallel
    var tasks = inputs.Select(input => 
        context.CallFunction<string>("ProcessItem", input)
    ).ToArray();
    
    // Fan-in: Wait for all to complete
    var results = await Task.WhenAll(tasks);
    
    return string.Join(", ", results);
});
```

### Human-in-the-Loop
Wait for external events (like user approval):

```csharp
runtime.RegisterOrchestratorFunction<string, string>("ApprovalPattern", async context =>
{
    var request = context.GetInput<string>();
    
    // Send approval request
    await context.CallFunction("SendApprovalRequest", request);
    
    // Wait for human decision
    var approved = await context.WaitForExternalEvent<bool>("ApprovalEvent");
    
    if (approved)
    {
        return await context.CallFunction<string>("ProcessApproval", request);
    }
    else
    {
        return await context.CallFunction<string>("HandleRejection", request);
    }
});
```

### Timer-Based Workflows
Handle delays and scheduling:

```csharp
runtime.RegisterOrchestratorFunction<string, string>("TimerPattern", async context =>
{
    var customerEmail = context.GetInput<string>();
    
    // Send welcome email immediately
    await context.CallFunction("SendWelcomeEmail", customerEmail);
    
    // Wait 24 hours
    var followUpTime = context.CurrentUtcDateTime.AddHours(24);
    await context.CreateTimer(followUpTime);
    
    // Send follow-up email
    await context.CallFunction("SendFollowUpEmail", customerEmail);
    
    return "Email sequence completed";
});
```

## 🏛️ Architecture Principles

### Single Responsibility
- **Orchestrators** coordinate workflow logic
- **Activities** perform specific business operations
- **State stores** handle persistence
- **Runtime** manages execution

### Separation of Concerns
```csharp
// ✅ Good separation
public class OrderWorkflow
{
    // Orchestrator focuses on coordination
    public async Task<string> ProcessOrder(IDurableOrchestrationContext context)
    {
        var order = context.GetInput<Order>();
        
        var validated = await context.CallFunction<Order>("ValidateOrder", order);
        var charged = await context.CallFunction<Order>("ChargePayment", validated);
        var shipped = await context.CallFunction<Order>("ShipOrder", charged);
        
        return $"Order {order.Id} processed";
    }
}

public class OrderActivities
{
    // Activities focus on specific business logic
    public async Task<Order> ValidateOrder(Order order)
    {
        // Validation logic here
        return order;
    }
    
    public async Task<Order> ChargePayment(Order order)
    {
        // Payment processing logic here
        return order;
    }
}
```

### Fault Isolation
- Failed activities don't crash the entire orchestration
- Retries are automatic and configurable
- State is preserved across failures

## 💾 State Management

### Checkpointing
The runtime automatically checkpoints state:
- After each successful activity completion
- Before waiting for external events
- Before creating timers

### State Serialization
- State is serialized to JSON by default
- Custom serializers can be configured
- State should be serializable and versionable

### State Versioning
Design your state classes for evolution:

```csharp
public class OrderState
{
    public string Id { get; set; }
    public decimal Amount { get; set; }
    
    // ✅ Good: Optional properties for backward compatibility
    public string CustomerEmail { get; set; } = "";
    public DateTime? ProcessedAt { get; set; }
    
    // ✅ Good: Version field for explicit versioning
    public int Version { get; set; } = 1;
}
```

## 🎯 Best Practices

### Orchestrator Design
1. **Keep orchestrators simple** - Focus on coordination, not business logic
2. **Use meaningful names** - Clear function and orchestrator names
3. **Handle errors gracefully** - Plan for activity failures
4. **Design for idempotency** - Activities should be safe to retry

### Activity Design
1. **Make activities focused** - Single responsibility per activity
2. **Design for retries** - Activities should be idempotent when possible
3. **Use proper error handling** - Throw appropriate exceptions
4. **Limit execution time** - Long-running activities should be split

### State Design
1. **Keep state minimal** - Only store what's necessary
2. **Design for evolution** - Plan for schema changes
3. **Use value objects** - Immutable data structures when possible
4. **Avoid circular references** - Keep serialization simple

## 🔍 Understanding Execution Flow

1. **Trigger** - Orchestration is triggered with an instance ID and input
2. **Schedule** - Runtime schedules the orchestrator for execution
3. **Execute** - Orchestrator runs, calling activities and managing state
4. **Checkpoint** - State is persisted after each successful step
5. **Wait/Complete** - Orchestration waits for events or completes
6. **Replay** - On resume, orchestration replays to reconstruct state

This execution model ensures reliability and consistency across failures and restarts.

---

Ready to dive deeper? Next, explore [Orchestrators](orchestrators.md) to learn advanced orchestration patterns!