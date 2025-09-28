# OpenTelemetry Integration Guide

This guide shows how to configure OpenTelemetry distributed tracing for Asynkron.DurableFunctions.

## Overview

The framework now includes built-in OpenTelemetry tracing via an `ActivitySource` named `"Asynkron.DurableFunctions"`. This provides visibility into:

- Orchestration lifecycle (start, complete, failure)
- Function and activity calls
- State persistence operations
- External event handling
- Error conditions and performance metrics

## Configuration

### 1. Install Required Packages

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.Console  # For testing
dotnet add package OpenTelemetry.Exporter.Jaeger   # For production
```

### 2. Configure OpenTelemetry in Program.cs

```csharp
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracingBuilder =>
    {
        tracingBuilder
            .AddSource("Asynkron.DurableFunctions")  // Enable DurableFunctions tracing
            .AddAspNetCoreInstrumentation()          // Optional: ASP.NET Core tracing
            .AddHttpClientInstrumentation()         // Optional: HTTP client tracing
            .AddConsoleExporter()                   // For development
            .AddJaegerExporter();                   // For production
    });

var app = builder.Build();

app.UseRouting();
app.UseDurableTraceContext();
app.MapControllers();
```

### 3. Ensure HTTP Trace Propagation

Place `UseDurableTraceContext()` before mapping the management controllers so incoming W3C headers populate `Activity.Current`. This keeps the caller's trace ID attached to orchestration state and controller spans.

```csharp
app.UseRouting();
app.UseDurableTraceContext();
app.MapControllers();
```

### 4. Configure Jaeger (Production)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracingBuilder =>
    {
        tracingBuilder
            .AddSource("Asynkron.DurableFunctions")
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "your-jaeger-host";
                options.AgentPort = 6831;
            });
    });
```

### 5. Environment Variables for Jaeger

```bash
export OTEL_EXPORTER_JAEGER_AGENT_HOST=localhost
export OTEL_EXPORTER_JAEGER_AGENT_PORT=6831
export OTEL_SERVICE_NAME=my-durable-functions-app
```

## Trace Spans Reference

The framework generates the following trace spans:

### Orchestration Spans

| Span Name | Description | Tags |
|-----------|-------------|------|
| `orchestration.start` | Orchestrator instance execution begins | `function.name`, `instance.id`, `parent.instance.id` |
| `orchestration.complete` | Orchestrator instance completes | `function.name`, `instance.id`, `orchestration.timer_was_set`, `orchestration.state_deleted` |
| `orchestration.call` | Function or sub-orchestrator call | `function.name`, `instance.id`, `parent.instance.id`, `input.type` |

### Management API Spans

| Span Name | Description | Tags |
|-----------|-------------|------|
| `Management.StartOrchestration` | HTTP start endpoint accepted a request | `durable.management.orchestrator`, `durable.management.response.instance_id` |
| `Management.GetStatus` | HTTP status query executed | `durable.management.instance_id`, `durable.management.response.runtime_status` |
| `Management.RaiseEvent` | HTTP external event accepted | `durable.management.instance_id`, `durable.management.event_name` |
| `Management.Terminate` | HTTP termination command accepted | `durable.management.instance_id`, `durable.management.terminate.reason` |
| `Management.Purge` | HTTP purge call completed | `durable.management.instance_id`, `durable.management.response.deleted` |

### Event and Messaging Spans

| Span Name | Description | Tags |
|-----------|-------------|------|
| `orchestration.event.receive` | External event raised to orchestration | `instance.id`, `event.name`, `payload.type`, `event.queue_depth` |

### State Management Spans

| Span Name | Description | Tags |
|-----------|-------------|------|
| `state.save` | State persistence operation | `instance.id`, `function.name` |
| `state.load` | State retrieval operation | `instance.id`, `function.name` |
| `state.remove` | State deletion operation | `instance.id`, `function.name` |

## Example Usage

```csharp
// Register your functions
runtime.RegisterOrchestrator<string, string>("ProcessOrder", async (context, input) =>
{
    // This creates orchestration.start span
    var paymentResult = await context.CallAsync<string>("ProcessPayment", input);
    
    // This creates orchestration.call span
    var shippingResult = await context.CallAsync<string>("ArrangeShipping", paymentResult);
    
    return shippingResult;
    // This creates orchestration.complete span
});

runtime.RegisterFunction<string, string>("ProcessPayment", input =>
{
    // This creates orchestration.call span
    return Task.FromResult($"Payment processed: {input}");
});

// Start orchestration - creates state.save span
await runtime.TriggerAsync("", "ProcessOrder", "order-123");

// Raise external event - creates orchestration.event.receive span
await runtime.RaiseEventAsync("instance-id", "ApprovalReceived", new { approved = true });
```

## Correlation and Context Propagation

Activities automatically flow across async boundaries, allowing you to:

- Correlate orchestrator execution with child activities
- Track request flows across service boundaries
- Debug complex orchestration scenarios
- Monitor performance bottlenecks

## Sampling and Performance

To control trace volume and performance impact:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracingBuilder =>
    {
        tracingBuilder
            .AddSource("Asynkron.DurableFunctions")
            .SetSampler(new TraceIdRatioBasedSampler(0.1)) // Sample 10% of traces
            .AddJaegerExporter();
    });
```

## Viewing Traces

### Console Output (Development)
With console exporter, traces appear in your application logs:

```
Activity.TraceId:            80dc914b885765d1aa22b18e1571efb1
Activity.SpanId:             6dcf3ea4a96516db
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       a6fb4c84fcb9e655
Activity.ActivitySourceName: Asynkron.DurableFunctions
Activity.DisplayName:        orchestration.start
Activity.Kind:               Internal
Activity.StartTime:          2024-01-15T10:30:00.000Z
Activity.Duration:           00:00:00.1234567
Activity.Tags:
    function.name: ProcessOrder
    instance.id: F8E2C4B6A1D34E9F8A5B2C7D3E8F9A0B1C2D3E4F5A6B7C8D9E0F1A2B3C4D5E6F7
```

### Jaeger UI (Production)
Navigate to your Jaeger UI (typically http://localhost:16686) to see:

- Service maps showing orchestration dependencies
- Timeline views of orchestration execution
- Error rates and performance metrics
- Distributed traces across services

### Jaeger / OTLP Quickstart

The `Asynkron.DurableFunctions.Examples` project ships with a ready-made sample that exports traces to the default OTLP
collector port (4317). Spin up the docker compose stack with the bundled Jaeger service, then run:

```bash
dotnet run --project examples/Asynkron.DurableFunctions.Examples.csproj -- otel
```

This triggers a small demo orchestrator and streams spans to the collector so you can inspect them in the Jaeger UI
at http://localhost:16686.

> Looking for persistence spans? State save/load/remove spans are disabled by default to keep traces focused on
> business operations. Set `runtimeOptions.TraceStateOperations = true;` when creating `DurableFunctionRuntime`
> if you need that detail.

## Best Practices

1. **Use meaningful operation names**: The framework provides descriptive span names out of the box
2. **Add custom tags for business context**: Consider adding business-specific tags to spans
3. **Monitor performance impact**: Tracing adds minimal overhead (<5%) but monitor in production
4. **Configure appropriate sampling**: Use sampling to control trace volume in high-throughput scenarios
5. **Correlate with logs**: Use trace IDs to correlate with your application logs

## Troubleshooting

### No traces appearing
- Verify the ActivitySource name is exactly `"Asynkron.DurableFunctions"`
- Check that OpenTelemetry is configured before creating the DurableFunctionRuntime
- Ensure your exporter configuration is correct

### High overhead
- Implement sampling to reduce trace volume
- Check your exporter configuration for batching settings
- Monitor resource usage and adjust sampling rates

### Missing context propagation
- Ensure you're using async/await patterns correctly
- Verify that the current Activity context is flowing through your code
- Check for any custom thread creation that might break context flow

## Integration with Existing Observability

The ActivitySource integrates seamlessly with:

- **Application Insights**: Automatic correlation with ASP.NET Core traces
- **Prometheus**: Use with OpenTelemetry metrics exporters
- **Grafana**: Combine with Tempo for trace visualization
- **Elasticsearch**: Export traces to ELK stack via OTLP

For questions or issues, please refer to the main project documentation or create an issue on GitHub.
