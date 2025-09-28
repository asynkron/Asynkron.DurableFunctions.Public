# SQLite StateStore and Enhanced Activity Tracking

This guide covers the new SQLite StateStore implementation and enhanced ActivityResults tracking that provides detailed debugging information for durable function workflows.

## Overview

The enhancements include:

1. **SQLite StateStore**: A persistent storage implementation of `IStateStore` using SQLite
2. **Enhanced Activity Tracking**: Detailed status information for all activities including initiation timestamps, function names, inputs, and completion status

## Enhanced Activity Tracking

### ActivationValue Record

Activities are now tracked using the `ActivationValue` record which contains:

```csharp
public record ActivationValue(
    DateTimeOffset InitiatedAt,    // When the activity was started
    string? Result,                // The result (null if not completed)
    string FunctionName,           // Name of the target function
    string Input                   // Raw input string
)
{
    public bool IsCompleted => Result is not null;
}
```

### Benefits

- **Debugging Support**: See exactly when activities started and what they're processing
- **Parallel Task Visibility**: Distinguish between initiated and completed activities
- **Rich Status Information**: Track function names, inputs, and execution timing
- **Production Monitoring**: Query state to understand workflow progress

## SQLite StateStore

### Basic Usage

```csharp
using var stateStore = new SqliteStateStore("Data Source=functions.db");
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);
```

### Connection Options

**Connection String:**
```csharp
var stateStore = new SqliteStateStore("Data Source=functions.db", logger);
```

**Existing Connection:**
```csharp
using var connection = new SqliteConnection("Data Source=:memory:");
var stateStore = new SqliteStateStore(connection, logger);
```

**In-Memory Database (Testing):**
```csharp
var stateStore = new SqliteStateStore("Data Source=:memory:", logger);
```

### Database Schema

The SQLite implementation creates the following table:

```sql
CREATE TABLE DurableFunctionStates (
    InstanceId TEXT PRIMARY KEY,
    FunctionName TEXT NOT NULL,
    Input TEXT NOT NULL,
    ExecuteAfter TEXT NOT NULL,
    ParentInstanceId TEXT,
    ActivityResults TEXT NOT NULL,    -- JSON serialized ActivationValue dictionary
    HistoryEvents TEXT NOT NULL,      -- JSON serialized history events
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

Indexes are automatically created for optimal performance:
- `IX_DurableFunctionStates_ExecuteAfter` - For polling ready states
- `IX_DurableFunctionStates_FunctionName` - For function-based queries
- `IX_DurableFunctionStates_ParentInstanceId` - For hierarchical queries

## Debugging Workflows

### Querying Activity Status

```csharp
// Get all current states
var states = await stateStore.GetReadyStatesAsync(DateTimeOffset.MaxValue);

foreach (var state in states)
{
    Console.WriteLine($"Orchestrator: {state.FunctionName} ({state.InstanceId})");
    
    foreach (var activity in state.ActivityResults)
    {
        var value = activity.Value;
        var status = value.IsCompleted ? "COMPLETED" : "IN PROGRESS";
        var elapsed = DateTimeOffset.UtcNow - value.InitiatedAt;
        
        Console.WriteLine($"  {value.FunctionName}: {status} ({elapsed.TotalSeconds:F1}s)");
        Console.WriteLine($"    Input: {value.Input}");
        
        if (value.IsCompleted)
        {
            Console.WriteLine($"    Result: {value.Result}");
        }
    }
}
```

### Monitoring Parallel Execution

```csharp
runtime.RegisterOrchestrator("ParallelWorkflow", async context =>
{
    // Start multiple activities
    var task1 = context.CallActivityAsync<string>("ProcessData", "data1");
    var task2 = context.CallActivityAsync<string>("ProcessData", "data2");
    var task3 = context.CallActivityAsync<string>("TransformData", "data3");
    
    // All activities are now tracked with initiation timestamps
    // You can query the state to see which are running vs completed
    
    var results = await Task.WhenAll(task1, task2, task3);
    return string.Join(", ", results);
});
```

## Migration from InMemoryStateStore

The SQLite StateStore is a drop-in replacement for InMemoryStateStore:

**Before:**
```csharp
var stateStore = new InMemoryStateStore();
var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);
```

**After:**
```csharp
var stateStore = new SqliteStateStore("Data Source=functions.db", logger);
var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);
```

All existing orchestrators and activities work without modification.

## Additional Methods

The SQLite StateStore provides additional utility methods:

```csharp
// Get count of stored states
int count = await stateStore.GetCountAsync();

// Clear all states (useful for testing)
await stateStore.ClearAsync();

// Proper disposal
stateStore.Dispose(); // or use 'using' statement
```

## Performance Considerations

- **Indexing**: Automatic indexes optimize common query patterns
- **JSON Storage**: Complex objects are stored as JSON for flexibility
- **Connection Management**: Supports both owned and shared connections
- **Async Operations**: All operations are fully asynchronous

## Testing

For unit tests, use in-memory databases:

```csharp
[Fact]
public async Task TestOrchestrator()
{
    using var stateStore = new SqliteStateStore("Data Source=:memory:");
    var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);
    
    // Test your orchestrators...
    
    // Verify final state
    Assert.Equal(0, await stateStore.GetCountAsync());
}
```

## Example

See `examples/SqliteExample.cs` for a complete working example demonstrating:
- SQLite StateStore setup
- Parallel activity execution
- Real-time state monitoring
- Activity status tracking

Run the example with:
```bash
dotnet run --project examples/
```
