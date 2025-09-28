# Asynkron.DurableFunctions Vocabulary

This document describes the key concepts and components in the function call state foundation and how they work together
to enable idempotent activity invocation in durable orchestrations.

## Core Concepts

### Orchestrator

A durable function that coordinates the execution of activities and manages workflow state. Orchestrators can pause and
resume execution, making them resilient to failures and restarts.

### Activity

A unit of work that performs a specific task within an orchestration. Functions are idempotent and can be retried
safely without side effects. Invoked using **CallFunction**.

### Durable Function State

The persistent state of a function that includes execution timing, input data, and cached activity results. This state
survives across process restarts and enables replay scenarios.

## Function Call State Foundation Components

### Instance ID

A unique, deterministic identifier generated from `[orchestratorStateInstanceId, activityName, parameters]`. This ID
ensures that the same activity call with the same parameters always produces the same identifier, enabling idempotent
execution.

```csharp
// Example: Calling F1 with null parameters from a specific orchestrator state
// Always generates the same hash: "93301F421FE97649C0CC9A426EF6F6450D949D9B431312079097D1154B0F6709"
var instanceId = DurableFunctionState.GenerateChildInstanceId(stateInstanceId, "F1", null);
```

### Activity Results Cache

A dictionary stored in `DurableFunctionState.ActivityResults` that maps instance IDs to their corresponding results.
This cache enables functions called via **CallFunction** to return immediately on subsequent calls without re-execution.

```csharp
// Structure: Dictionary<InstanceId, SerializedResult>
state.ActivityResults["93301F..."] = ""Result from F1""; // JSON serialized
```

### WaitingForStateUpdateException

A special exception thrown by orchestrators when they need to wait for a function result that isn't yet available. This
exception triggers the runtime to pause the orchestrator and reschedule it for later execution.

```csharp
// Thrown when activity result is not cached
throw new WaitingForStateUpdateException(instanceId);
```

### Parent Instance ID

A reference stored in function state that points back to the orchestrator invocation that triggered the function. This
enables result propagation back to the waiting orchestrator.

```csharp
// Activity state tracks which orchestrator is waiting for its result
activityState.ParentInstanceId = orchestratorInstanceId;
```

### Instance ID Generation Process

The system uses SHA256 hashing to generate deterministic instance IDs that ensure idempotent execution:

```mermaid
flowchart LR
    A[Parent InstanceId] --> D[SHA256 Hash]
    B[Function Name] --> D
    C[Serialized Parameters] --> D
    D --> E[Hex String<br/>Instance ID]
    
    subgraph Example
        F["Parent: ''<br/>Function: 'F1'<br/>Params: null"] --> G[SHA256]
        G --> H["93301F421FE97649C0CC9A...<br/>(64 character hex)"]
    end
    
    style D fill:#fff2cc
    style G fill:#fff2cc
```

The `GenerateChildInstanceId` method ensures that:

- Same inputs always produce same ID (deterministic)
- Different inputs produce different IDs (unique)
- Parent-child relationships are maintained through the parent instance ID

## How Components Work Together

### 1. Function Invocation Flow

```mermaid
flowchart TD
    A[Orchestrator calls<br/>CallFunction] --> B[Generate Child Instance ID<br/>Hash of ParentInstanceId, FunctionName, Parameters]
    B --> C{Check ActivityResults<br/>Cache for Result}
    C -->|Found| D[Return Cached Result<br/>Immediately]
    C -->|Not Found| E[Create Function State<br/>with ParentInstanceId]
    E --> F[Trigger Function Execution]
    F --> G[Throw WaitingForStateUpdateException<br/>with InstanceId]
    G --> H[Runtime Catches Exception<br/>& Pauses Orchestrator]
    
    style C fill:#fff2cc
    style D fill:#d5e8d4
    style G fill:#f8cecc
```

### 2. Orchestrator Pause-Resume Cycle

```mermaid
flowchart TD
    A[WaitingForStateUpdateException<br/>Thrown] --> B[Runtime Catches Exception<br/>& Extracts InstanceId]
    B --> C[Reschedule Orchestrator<br/>for Future Execution]
    C --> D[Function Execution<br/>Continues Independently]
    D --> E[Function Completes<br/>& Result Available]
    E --> F[PropagateActivityResultAsync<br/>Called]
    F --> G[Scan All Orchestrator States<br/>for Matching ParentInstanceId]
    G --> H[Update ActivityResults&#91;InstanceId&#93;<br/>with JSON Result]
    H --> I[Reschedule Orchestrator<br/>to Run Immediately]
    I --> J[Orchestrator Re-executed<br/>from Last State]
    J --> K{Check ActivityResults<br/>Cache Again}
    K -->|Found| L[Resume with Cached Result<br/>Continue Execution]
    K -->|Not Found| M[Wait for More Results]
    
    style A fill:#f8cecc
    style E fill:#d5e8d4
    style L fill:#d5e8d4
```

### 3. Function Result Propagation

```mermaid
flowchart TD
    A[Function Completes] --> B[Function Result Available]
    B --> C[Find Waiting Orchestrators]
    C --> D[Update ActivityResults Cache]
    D --> E[Reschedule Orchestrator Immediately]
```

### 4. Complete Function Interaction via Persistent State Store

```mermaid
flowchart TD
    subgraph Persistent State Store
        SS[(State Store)]
        OS1[Orchestrator State<br/>InstanceId: ABC123<br/>ActivityResults: Map]
        AS1[Activity State<br/>InstanceId: DEF456<br/>ParentInstanceId: ABC123]
        AS2[Activity State<br/>InstanceId: GHI789<br/>ParentInstanceId: ABC123]
    end
    
    subgraph Runtime Execution
        OR[Orchestrator<br/>ChainedWorkflow]
        A1[Activity F1]
        A2[Activity F2]
        A3[Activity F3]
        A4[Activity F4]
    end
    
    subgraph Execution Flow
        OR -->|1. CallActivity F1| A1
        A1 -->|2. Result propagated<br/>via ParentInstanceId| OS1
        OS1 -->|3. Resume with<br/>cached result| OR
        OR -->|4. CallActivity F2| A2
        A2 -->|5. Result propagated<br/>back to orchestrator| OS1
        OS1 -->|6. Resume execution| OR
        OR -->|7. CallActivity F3| A3
        A3 -->|8. Result propagated| OS1
        OS1 -->|9. Resume execution| OR
        OR -->|10. CallActivity F4| A4
        A4 -->|11. Final result<br/>propagated| OS1
    end
    
    SS -.->|Persist/Retrieve| OS1
    SS -.->|Persist/Retrieve| AS1
    SS -.->|Persist/Retrieve| AS2
    
    style SS fill:#e1f5fe
    style OR fill:#f3e5f5
    style A1,A2,A3,A4 fill:#e8f5e8
```

## Practical Example: Chained Function Workflow

Here's a practical example showing how a chained function workflow interacts with the persistent state store:

```csharp
// Orchestrator function that demonstrates the complete pattern
public static async Task<string> ChainedWorkflow(IDurableOrchestrationContext context)
{
    try
    {
        // Each CallFunction call generates a unique instance ID and checks cache
        var x = await context.CallFunction<string>("F1", null);
        var y = await context.CallFunction<string>("F2", x);
        var z = await context.CallFunction<string>("F3", y);
        return await context.CallFunction<string>("F4", z);
    }
    catch (Exception ex)
    {
        // Error handling or compensation logic
        return $"Failed: {ex.Message}";
    }
}
```

### Execution Timeline

```mermaid
gantt
    title Durable Function Execution Timeline
    dateFormat X
    axisFormat %L
    
    section Orchestrator
    Start Orchestration    :0, 1
    Wait for F1           :1, 3
    Resume & Call F2      :3, 4
    Wait for F2           :4, 6
    Resume & Call F3      :6, 7
    Wait for F3           :7, 9
    Resume & Call F4      :9, 10
    Wait for F4           :10, 12
    Complete              :12, 13
    
    section Activities
    F1 Execution          :2, 3
    F2 Execution          :5, 6
    F3 Execution          :8, 9
    F4 Execution          :11, 12
```

### State Store Interactions

During this workflow, the state store maintains:

1. **Orchestrator State**: Contains the execution timeline and function results cache
2. **Function States**: Each function maintains a reference back to the orchestrator
3. **Result Propagation**: Completed functions update the orchestrator's cache and trigger resumption

This architecture enables:

- **Resilience**: Process crashes don't lose progress
- **Scalability**: Orchestrators don't block threads while waiting
- **Consistency**: Replay guarantees deterministic execution

## Key Patterns

### Idempotent Function Pattern

Functions with the same instance ID are never executed more than once. The system automatically returns cached results
for repeated calls using **CallFunction**.

### Deterministic Replay

Orchestrators can be replayed from any point in their execution history because all function results are cached and
deterministically retrieved via **CallFunction**.

### Asynchronous Coordination

Orchestrators don't block waiting for functions. Instead, they pause execution and resume when results become
available, allowing the runtime to process other work.

## State Store Architecture

The persistent state store is the central component that enables function coordination and recovery. Here's how the
different components interact with it:

```mermaid
flowchart TB
    subgraph DurableFunctionRuntime
        RT[Runtime Engine]
        RF[RegisterJsonFunction]
        RO[RegisterOrchestrator]
        TR[TriggerAsync]
        EF[ExecuteFunctionAsync]
        PR[PropagateActivityResultAsync]
    end
    
    subgraph State Store Interface
        SS[(IStateStore)]
        SSS[SaveStateAsync]
        GSS[GetStateAsync]
        GRS[GetReadyStatesAsync]
        RSS[RemoveStateAsync]
    end
    
    subgraph Persistent Storage
        OS[Orchestrator States<br/>- InstanceId<br/>- ActivityResults Cache<br/>- ExecuteAfter]
        AS[Activity States<br/>- InstanceId<br/>- ParentInstanceId<br/>- Input/Output]
        HS[History Events<br/>- Execution Timeline<br/>- Replay Information]
    end
    
    subgraph Execution Context
        DOC[DurableOrchestrationContext]
        CAA[CallActivityAsync]
        WFS[WaitingForStateUpdateException]
    end
    
    RT --> SS
    TR --> SSS
    EF --> SSS
    EF --> GSS
    PR --> GRS
    PR --> SSS
    
    SS --> OS
    SS --> AS
    SS --> HS
    
    DOC --> CAA
    CAA --> WFS
    WFS --> RT
    
    style SS fill:#e1f5fe
    style OS fill:#f3e5f5
    style AS fill:#e8f5e8
    style WFS fill:#ffebee
```

## State Persistence

### DurableFunctionState Properties

- `ExecuteAfter`: When the function should next run
- `InstanceId`: Hash-based identifier for the function state
- `FunctionName`: Name of the function to be executed
- `Input`: Serialized input parameters as JSON
- `ActivityResults`: Dictionary of cached activity results keyed by instance ID
- `ParentInstanceId`: Reference to parent orchestrator (for activities)
- `HistoryEvents`: List of execution history events for replay scenarios

### State Store Interface

The `IStateStore` provides persistence operations:

- `SaveStateAsync()`: Persist function state
- `GetStateAsync()`: Retrieve function state by ID
- `GetReadyStatesAsync()`: Find functions ready for execution
- `RemoveStateAsync()`: Clean up completed functions

## Error Handling

### WaitingForStateUpdateException

- **Purpose**: Signal that orchestrator needs to wait for activity completion
- **Handling**: Runtime reschedules orchestrator for future execution
- **Recovery**: Orchestrator resumes when activity result becomes available

### Function Failures

Function failures are propagated back to orchestrators as normal exceptions, allowing orchestrators to implement
compensation logic or retry patterns.

## Performance Considerations

### Memory Efficiency

- Function results are stored as JSON strings to minimize memory usage
- Instance IDs use SHA256 for uniqueness while maintaining reasonable size
- Completed orchestrations are automatically cleaned up from state storage

### Execution Efficiency

- Cached results eliminate redundant function executions
- Orchestrator pause-resume minimizes resource usage while waiting
- Result propagation enables immediate orchestrator resumption

This vocabulary provides the foundation for understanding how the function call state system enables reliable,
efficient, and scalable durable orchestrations.
