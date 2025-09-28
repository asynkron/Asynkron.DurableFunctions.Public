# Orchestrator Functions

Orchestrator functions are the heart of durable workflows. They coordinate the execution of activities, manage state, and define the business logic flow.

## 🎭 What is an Orchestrator?

An orchestrator is a function that:
- **Coordinates workflow execution** - Calls activities in the right order
- **Manages workflow state** - Maintains data across long-running processes
- **Handles timing** - Creates durable timers and delays
- **Processes events** - Waits for external events and human input
- **Ensures reliability** - Survives failures and restarts through replay

## 📝 Registering Orchestrators

### Basic Registration
```csharp
runtime.RegisterOrchestratorFunction<InputType, OutputType>("OrchestratorName", 
    async context =>
    {
        var input = context.GetInput<InputType>();
        // Orchestration logic here
        return output;
    });
```

### Strongly Typed Registration
```csharp
public class OrderRequest
{
    public string Id { get; set; }
    public decimal Amount { get; set; }
    public string CustomerEmail { get; set; }
}

public class OrderResult
{
    public string OrderId { get; set; }
    public string Status { get; set; }
    public DateTime CompletedAt { get; set; }
}

runtime.RegisterOrchestratorFunction<OrderRequest, OrderResult>("ProcessOrder", 
    async context =>
    {
        var order = context.GetInput<OrderRequest>();
        
        // Process the order...
        
        return new OrderResult
        {
            OrderId = order.Id,
            Status = "Completed",
            CompletedAt = DateTime.UtcNow
        };
    });
```

## 🧩 Orchestration Context

The `context` parameter provides access to orchestration capabilities:

### Getting Input Data
```csharp
var input = context.GetInput<MyInputType>();
```

### Calling Activity Functions
```csharp
// Simple call
var result = await context.CallFunction<OutputType>("FunctionName", input);

// With error handling
try
{
    var result = await context.CallFunction<OutputType>("RiskyFunction", input);
}
catch (Exception ex)
{
    // Handle activity failure
    return await context.CallFunction<string>("HandleError", ex.Message);
}
```

### Creating Durable Timers
```csharp
// Wait for a specific duration
var dueTime = context.CurrentUtcDateTime.AddMinutes(30);
await context.CreateTimer(dueTime);

// Wait until a specific time
var midnight = context.CurrentUtcDateTime.Date.AddDays(1);
await context.CreateTimer(midnight);
```

### Waiting for External Events
```csharp
// Wait for a simple event
var approved = await context.WaitForExternalEvent<bool>("ApprovalEvent");

// Wait for a complex event with timeout
using var cts = new CancellationTokenSource(TimeSpan.FromHours(24));
try
{
    var approval = await context.WaitForExternalEvent<ApprovalData>("DetailedApproval");
}
catch (OperationCanceledException)
{
    // Handle timeout
}
```

### Getting Current Time
```csharp
// Always use this instead of DateTime.Now
var currentTime = context.CurrentUtcDateTime;
```

### Logging
```csharp
// Get a replay-safe logger
var logger = context.GetLogger();
logger.LogInformation("Processing order {OrderId}", order.Id);

// Get a typed logger
var logger = context.GetLogger<MyOrchestrator>();
```

## 🎯 Orchestration Patterns

### 1. Sequential Processing
Execute activities one after another:

```csharp
runtime.RegisterOrchestratorFunction<OrderRequest, string>("SequentialOrder", 
    async context =>
    {
        var order = context.GetInput<OrderRequest>();
        
        // Each step waits for the previous to complete
        var validated = await context.CallFunction<OrderRequest>("ValidateOrder", order);
        var authorized = await context.CallFunction<OrderRequest>("AuthorizePayment", validated);
        var charged = await context.CallFunction<OrderRequest>("ChargePayment", authorized);
        var shipped = await context.CallFunction<OrderRequest>("ShipOrder", charged);
        var completed = await context.CallFunction<string>("CompleteOrder", shipped);
        
        return completed;
    });
```

### 2. Parallel Processing (Fan-out/Fan-in)
Execute multiple activities concurrently:

```csharp
runtime.RegisterOrchestratorFunction<string[], string>("ParallelProcessing", 
    async context =>
    {
        var items = context.GetInput<string[]>();
        
        // Start all activities in parallel
        var tasks = items.Select(item => 
            context.CallFunction<string>("ProcessItem", item)
        ).ToArray();
        
        // Wait for all to complete
        var results = await Task.WhenAll(tasks);
        
        // Combine results
        return string.Join(", ", results);
    });
```

### 3. Conditional Logic
Make decisions based on activity results:

```csharp
runtime.RegisterOrchestratorFunction<PaymentRequest, string>("ConditionalPayment", 
    async context =>
    {
        var payment = context.GetInput<PaymentRequest>();
        
        // Check if payment needs approval
        var needsApproval = await context.CallFunction<bool>("CheckApprovalNeeded", payment);
        
        if (needsApproval)
        {
            // Get approval first
            await context.CallFunction("RequestApproval", payment);
            var approved = await context.WaitForExternalEvent<bool>("ApprovalEvent");
            
            if (!approved)
            {
                return "Payment rejected";
            }
        }
        
        // Process the payment
        var result = await context.CallFunction<string>("ProcessPayment", payment);
        return result;
    });
```

### 4. Error Handling and Retry
Handle failures gracefully:

```csharp
runtime.RegisterOrchestratorFunction<string, string>("ResilientProcessing", 
    async context =>
    {
        var data = context.GetInput<string>();
        
        try
        {
            // Try the primary method
            return await context.CallFunction<string>("PrimaryProcessor", data);
        }
        catch (Exception ex)
        {
            // Log the error
            var logger = context.GetLogger();
            logger.LogWarning("Primary processor failed: {Error}", ex.Message);
            
            try
            {
                // Try the fallback method
                return await context.CallFunction<string>("FallbackProcessor", data);
            }
            catch (Exception fallbackEx)
            {
                // Log fallback failure and use default
                logger.LogError("Fallback processor also failed: {Error}", fallbackEx.Message);
                return await context.CallFunction<string>("DefaultProcessor", data);
            }
        }
    });
```

### 5. Long-Running Workflows with Timers
Handle processes that span days or weeks:

```csharp
runtime.RegisterOrchestratorFunction<string, string>("CustomerOnboarding", 
    async context =>
    {
        var customerEmail = context.GetInput<string>();
        var startTime = context.CurrentUtcDateTime;
        
        // Day 0: Send welcome email
        await context.CallFunction("SendWelcomeEmail", customerEmail);
        
        // Day 1: Send getting started guide
        await context.CreateTimer(startTime.AddDays(1));
        await context.CallFunction("SendGettingStartedGuide", customerEmail);
        
        // Day 7: Send weekly tips
        await context.CreateTimer(startTime.AddDays(7));
        await context.CallFunction("SendWeeklyTips", customerEmail);
        
        // Day 30: Send feedback survey
        await context.CreateTimer(startTime.AddDays(30));
        await context.CallFunction("SendFeedbackSurvey", customerEmail);
        
        return "Onboarding sequence completed";
    });
```

### 6. Human-in-the-Loop Workflows
Wait for human interaction:

```csharp
runtime.RegisterOrchestratorFunction<ExpenseRequest, string>("ExpenseApproval", 
    async context =>
    {
        var expense = context.GetInput<ExpenseRequest>();
        
        // Submit for approval
        await context.CallFunction("SubmitForApproval", expense);
        
        // Wait for manager decision with 48-hour timeout
        using var timeout = new CancellationTokenSource(TimeSpan.FromHours(48));
        
        try
        {
            var decision = await context.WaitForExternalEvent<ApprovalDecision>("ManagerDecision");
            
            if (decision.Approved)
            {
                await context.CallFunction("ProcessApprovedExpense", expense);
                return $"Expense {expense.Id} approved and processed";
            }
            else
            {
                await context.CallFunction("NotifyRejection", expense, decision.Reason);
                return $"Expense {expense.Id} rejected: {decision.Reason}";
            }
        }
        catch (OperationCanceledException)
        {
            // Auto-reject on timeout
            await context.CallFunction("HandleApprovalTimeout", expense);
            return $"Expense {expense.Id} rejected due to timeout";
        }
    });
```

## 🚨 Critical Rules for Orchestrators

### ✅ Do's

1. **Use context methods for time**:
```csharp
// ✅ Good
var dueTime = context.CurrentUtcDateTime.AddMinutes(5);
```

2. **Call external services through activities**:
```csharp
// ✅ Good
var data = await context.CallFunction<string>("CallExternalAPI", request);
```

3. **Use deterministic operations**:
```csharp
// ✅ Good - always produces same result
var uppercased = input.ToUpperInvariant();
```

4. **Handle exceptions appropriately**:
```csharp
// ✅ Good
try
{
    var result = await context.CallFunction<string>("RiskyOperation", input);
    return result;
}
catch (BusinessException ex)
{
    // Handle specific business errors
    return await context.CallFunction<string>("HandleBusinessError", ex.Message);
}
```

### ❌ Don'ts

1. **Don't use DateTime.Now or DateTime.UtcNow**:
```csharp
// ❌ Bad - non-deterministic
var now = DateTime.Now;
```

2. **Don't call external services directly**:
```csharp
// ❌ Bad - non-deterministic and not replay-safe
var httpClient = new HttpClient();
var response = await httpClient.GetStringAsync("https://api.example.com");
```

3. **Don't use random numbers directly**:
```csharp
// ❌ Bad - non-deterministic
var randomValue = Random.Shared.Next();
```

4. **Don't perform I/O operations directly**:
```csharp
// ❌ Bad - non-deterministic
var content = await File.ReadAllTextAsync("config.json");
```

## 🏗️ Advanced Patterns

### Sub-Orchestrations
Call other orchestrators as sub-workflows:

```csharp
runtime.RegisterOrchestratorFunction<BatchRequest, string>("BatchProcessor", 
    async context =>
    {
        var batch = context.GetInput<BatchRequest>();
        
        // Process each item using a sub-orchestrator
        var tasks = batch.Items.Select(item =>
            context.CallSubOrchestratorAsync<string>("ProcessSingleItem", item)
        ).ToArray();
        
        var results = await Task.WhenAll(tasks);
        
        return $"Batch completed: {results.Length} items processed";
    });
```

### Event-Driven State Machines
Build complex state machines with events:

```csharp
runtime.RegisterOrchestratorFunction<OrderRequest, string>("OrderStateMachine", 
    async context =>
    {
        var order = context.GetInput<OrderRequest>();
        var state = "Created";
        
        while (state != "Completed" && state != "Cancelled")
        {
            switch (state)
            {
                case "Created":
                    await context.CallFunction("ValidateOrder", order);
                    state = "Validated";
                    break;
                    
                case "Validated":
                    var paymentResult = await context.CallFunction<string>("ProcessPayment", order);
                    state = paymentResult == "Success" ? "Paid" : "PaymentFailed";
                    break;
                    
                case "Paid":
                    await context.CallFunction("ShipOrder", order);
                    state = "Shipped";
                    break;
                    
                case "Shipped":
                    // Wait for delivery confirmation
                    var delivered = await context.WaitForExternalEvent<bool>("DeliveryConfirmation");
                    state = delivered ? "Completed" : "DeliveryFailed";
                    break;
                    
                case "PaymentFailed":
                case "DeliveryFailed":
                    state = "Cancelled";
                    break;
            }
        }
        
        return $"Order {order.Id} finished with state: {state}";
    });
```

## 📊 Monitoring and Debugging

### Logging Best Practices
```csharp
runtime.RegisterOrchestratorFunction<OrderRequest, string>("LoggingExample", 
    async context =>
    {
        var order = context.GetInput<OrderRequest>();
        var logger = context.GetLogger();
        
        logger.LogInformation("Starting order processing for {OrderId}", order.Id);
        
        try
        {
            var result = await context.CallFunction<string>("ProcessOrder", order);
            
            logger.LogInformation("Order {OrderId} processed successfully", order.Id);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process order {OrderId}", order.Id);
            throw;
        }
    });
```

### State Tracking
```csharp
runtime.RegisterOrchestratorFunction<ComplexRequest, string>("StateTrackingExample", 
    async context =>
    {
        var request = context.GetInput<ComplexRequest>();
        var logger = context.GetLogger();
        
        // Track progress through the workflow
        logger.LogInformation("Phase 1: Validation starting");
        var validated = await context.CallFunction<ComplexRequest>("Validate", request);
        logger.LogInformation("Phase 1: Validation completed");
        
        logger.LogInformation("Phase 2: Processing starting");
        var processed = await context.CallFunction<ComplexRequest>("Process", validated);
        logger.LogInformation("Phase 2: Processing completed");
        
        logger.LogInformation("Phase 3: Finalization starting");
        var finalized = await context.CallFunction<string>("Finalize", processed);
        logger.LogInformation("Phase 3: Finalization completed");
        
        return finalized;
    });
```

Orchestrators are powerful tools for building reliable, long-running workflows. Next, learn about [Activity Functions](activities.md) to understand how to implement the actual business logic!