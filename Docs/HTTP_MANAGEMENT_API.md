# HTTP Management API

This document describes the built-in HTTP APIs for orchestration management in Asynkron.DurableFunctions. These APIs provide similar functionality to Azure Durable Functions management endpoints, allowing you to start, query, terminate, and manage orchestrations via HTTP.

## Quick Setup

### 1. Add to your ASP.NET Core Application

```csharp
using Asynkron.DurableFunctions.Extensions;

var builder = WebApplication.CreateBuilder();

// Register your state store and runtime
builder.Services.AddSingleton<IStateStore>(/* your state store */);
builder.Services.AddSingleton<DurableFunctionRuntime>(/* configure runtime */);

// Add the built-in orchestration management APIs
builder.Services.AddDurableFunctionsManagement(options =>
{
    options.BaseUrl = "https://your-app.com"; // Optional: for generating management URLs
});

var app = builder.Build();
app.MapControllers();
```

### Trace Context & OpenTelemetry

To preserve distributed-trace headers that arrive on the management API, make sure you:

1. Register OpenTelemetry and listen to the Durable Functions activity source.
2. Add the `UseDurableTraceContext()` middleware *before* `MapControllers()`.

```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(res => res.AddService("durable-management-api"))
    .WithTracing(tracing => tracing
        .AddSource("Asynkron.DurableFunctions")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

// ...register state store + runtime...

var app = builder.Build();
app.UseRouting();
app.UseDurableTraceContext();
app.MapControllers();
```

With this setup the management endpoints forward `traceparent`, `tracestate`, and `baggage` headers into the orchestrator state. Controller actions additionally emit `Management.*` spans (e.g. `Management.StartOrchestration`) so you can see management operations alongside runtime execution in your trace viewer.

### 2. Alternative: Service-Only Registration

If you want to use the management service directly without the HTTP endpoints:

```csharp
builder.Services.AddDurableFunctionsManagementService("https://your-app.com");

// Then inject IOrchestrationManagementService where needed
```

## HTTP Endpoints

All endpoints are prefixed with `/runtime/orchestrations`.

### Start Orchestration

**Endpoint:** `POST /runtime/orchestrations/start/{orchestratorName}`

Starts a new orchestration instance.

**Request Body:**
```json
{
  "input": "any JSON object or primitive",
  "instanceId": "optional-custom-instance-id"
}
```

**Response:** `202 Accepted`
```json
{
  "instanceId": "generated-or-provided-instance-id",
  "managementUrls": {
    "statusQueryGetUri": "https://your-app.com/runtime/orchestrations/{instanceId}",
    "sendEventPostUri": "https://your-app.com/runtime/orchestrations/{instanceId}/raiseEvent/{eventName}",
    "terminatePostUri": "https://your-app.com/runtime/orchestrations/{instanceId}/terminate",
    "purgeHistoryDeleteUri": "https://your-app.com/runtime/orchestrations/{instanceId}"
  }
}
```

**Example:**
```bash
curl -X POST "https://localhost:5001/runtime/orchestrations/start/MyOrchestrator" \
     -H "Content-Type: application/json" \
     -d '{"input": {"name": "World", "count": 3}}'
```

### Get Orchestration Status

**Endpoint:** `GET /runtime/orchestrations/{instanceId}`

Gets the status of an orchestration instance.

**Query Parameters:**
- `showHistory` (bool): Include execution history (default: false)
- `showHistoryOutput` (bool): Include output in execution history (default: false)  
- `showInput` (bool): Include the original input (default: true)

**Response:** `200 OK` or `404 Not Found`
```json
{
  "instanceId": "instance-id",
  "name": "OrchestratorName",
  "runtimeStatus": "Running|Completed|Failed|Canceled|Terminated|Pending",
  "input": "original input object",
  "output": "result if completed",
  "createdTime": "2023-12-01T10:00:00Z",
  "lastUpdatedTime": "2023-12-01T10:05:00Z"
}
```

**Example:**
```bash
curl -X GET "https://localhost:5001/runtime/orchestrations/abc123?showInput=true"
```

### Send External Event

**Endpoint:** `POST /runtime/orchestrations/{instanceId}/raiseEvent/{eventName}`

Sends an external event to a running orchestration.

**Request Body:**
```json
{
  "eventData": "any JSON object or primitive"
}
```

**Response:** `202 Accepted`

**Example:**
```bash
curl -X POST "https://localhost:5001/runtime/orchestrations/abc123/raiseEvent/UserResponse" \
     -H "Content-Type: application/json" \
     -d '{"eventData": {"action": "approve", "comments": "Looks good!"}}'
```

### Terminate Orchestration

**Endpoint:** `POST /runtime/orchestrations/{instanceId}/terminate`

Terminates a running orchestration instance.

**Request Body:**
```json
{
  "reason": "Reason for termination"
}
```

**Response:** `202 Accepted`

**Example:**
```bash
curl -X POST "https://localhost:5001/runtime/orchestrations/abc123/terminate" \
     -H "Content-Type: application/json" \
     -d '{"reason": "User requested cancellation"}'
```

### Purge Instance History

**Endpoint:** `DELETE /runtime/orchestrations/{instanceId}`

Purges the history of an orchestration instance.

**Response:** `200 OK`
```json
{
  "instancesDeleted": 1
}
```

**Example:**
```bash
curl -X DELETE "https://localhost:5001/runtime/orchestrations/abc123"
```

## Error Responses

All endpoints return appropriate HTTP status codes:

- `400 Bad Request`: Invalid input parameters
- `404 Not Found`: Orchestration instance not found  
- `500 Internal Server Error`: Unexpected server error

Error responses include a message describing the issue:
```json
{
  "message": "Error description"
}
```

## Complete Example

Here's a complete ASP.NET Core application with the management APIs:

```csharp
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;

var builder = WebApplication.CreateBuilder();

// Configure services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add state store
builder.Services.AddSingleton<IStateStore>(provider =>
    new InMemoryStateStore());

// Add durable functions runtime
builder.Services.AddSingleton<DurableFunctionRuntime>(provider =>
{
    var stateStore = provider.GetRequiredService<IStateStore>();
    var logger = provider.GetRequiredService<ILogger<DurableFunctionRuntime>>();
    
    var runtime = new DurableFunctionRuntime(stateStore, logger);
    
    // Register your functions
    runtime.RegisterFunction<string, string>("SayHello", async name =>
        $"Hello, {name}!");
        
    runtime.RegisterOrchestrator<string, string>("GreetingOrchestrator", async context =>
    {
        var name = context.GetInput<string>();
        return await context.CallAsync<string>("SayHello", name);
    });
    
    return runtime;
});

// Add orchestration management APIs
builder.Services.AddDurableFunctionsManagement(options =>
{
    options.BaseUrl = "https://localhost:5001";
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

// Start background processing
var runtime = app.Services.GetRequiredService<DurableFunctionRuntime>();
_ = Task.Run(() => runtime.RunAndPollAsync());

await app.RunAsync();
```

## Programming Model

If you prefer to use the management service programmatically instead of HTTP endpoints:

```csharp
public class MyService
{
    private readonly IOrchestrationManagementService _managementService;
    
    public MyService(IOrchestrationManagementService managementService)
    {
        _managementService = managementService;
    }
    
    public async Task<string> StartWorkflow(string workflowName, object input)
    {
        var request = new StartOrchestrationRequest { Input = input };
        var response = await _managementService.StartOrchestrationAsync(workflowName, request);
        return response.InstanceId;
    }
    
    public async Task<OrchestrationStatus?> GetStatus(string instanceId)
    {
        return await _managementService.GetOrchestrationStatusAsync(instanceId);
    }
}
```

## Comparison to Azure Durable Functions

These APIs provide similar functionality to Azure Durable Functions HTTP management APIs:

| Azure Durable Functions | Asynkron.DurableFunctions |
|-------------------------|---------------------------|
| `POST .../orchestrators/{functionName}` | `POST /runtime/orchestrations/start/{orchestratorName}` |
| `GET .../instances/{instanceId}` | `GET /runtime/orchestrations/{instanceId}` |
| `POST .../instances/{instanceId}/raiseEvent/{eventName}` | `POST /runtime/orchestrations/{instanceId}/raiseEvent/{eventName}` |
| `POST .../instances/{instanceId}/terminate` | `POST /runtime/orchestrations/{instanceId}/terminate` |
| `DELETE .../instances/{instanceId}` | `DELETE /runtime/orchestrations/{instanceId}` |

The request/response formats are designed to be similar for easy migration.
