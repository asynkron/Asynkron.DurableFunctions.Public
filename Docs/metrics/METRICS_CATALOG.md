# Durable Functions Metrics Catalog

This document provides a comprehensive catalog of all metrics emitted by the Asynkron.DurableFunctions library.

## Overview

The library uses `System.Diagnostics.Metrics` API to emit OpenTelemetry-compatible metrics for monitoring orchestration performance, failures, and system health.

## Meter Information

- **Meter Name**: `Asynkron.DurableFunctions`
- **Version**: `1.0.0`

## Metrics Reference

### Orchestration Metrics

#### Counters

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `orchestrations.started` | Counter | orchestrations | Total orchestrations initiated | `orchestrator.name`, `instance.id`, `host.id` |
| `orchestrations.completed` | Counter | orchestrations | Total successful completions | `orchestrator.name`, `instance.id`, `host.id`, `status` |
| `orchestrations.failed` | Counter | orchestrations | Total failures | `orchestrator.name`, `instance.id`, `host.id`, `status`, `error.type` |

#### Histograms

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `orchestrations.duration` | Histogram | seconds | Time from start to completion | `orchestrator.name`, `instance.id`, `host.id`, `status` |

#### Gauges

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `orchestrations.active` | UpDownCounter | orchestrations | Currently running orchestrations | `orchestrator.name`, `instance.id`, `host.id` |

### Function Metrics

#### Counters

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `functions.calls` | Counter | calls | Individual function invocations | `function.name`, `instance.id`, `host.id` |
| `functions.failures` | Counter | failures | Function call failures | `function.name`, `instance.id`, `host.id`, `status`, `error.type` |

#### Histograms

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `functions.duration` | Histogram | seconds | Individual function execution time | `function.name`, `instance.id`, `host.id`, `status` |

### Event Metrics

#### Counters

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `events.raised` | Counter | events | External events raised | `event.name`, `instance.id`, `host.id` |
| `events.delivered` | Counter | events | External events delivered | `event.name`, `instance.id`, `host.id` |

### Timer Metrics

#### Counters

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `timers.created` | Counter | timers | Durable timers created | `instance.id`, `host.id` |
| `timers.fired` | Counter | timers | Timers that expired | `instance.id`, `host.id` |

### State Persistence Metrics

#### Histograms

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `state.save.duration` | Histogram | seconds | State persistence latency | `storage.type`, `instance.id`, `host.id` |
| `state.load.duration` | Histogram | seconds | State retrieval latency | `storage.type`, `instance.id`, `host.id` |

### Lease Management Metrics

#### Gauges

| Metric Name | Type | Unit | Description | Tags |
|-------------|------|------|-------------|------|
| `lease.active_count` | UpDownCounter | leases | Active lease count | `instance.id`, `host.id` |

## Tag Reference

### Standard Tags

| Tag Name | Description | Example Values |
|----------|-------------|----------------|
| `orchestrator.name` | Name of the orchestrator function | `"OrderProcessingOrchestrator"`, `"PaymentOrchestrator"` |
| `function.name` | Name of the activity function | `"SendEmail"`, `"ProcessPayment"` |
| `instance.id` | Unique identifier for the orchestration instance | `"order-12345"`, `"payment-67890"` |
| `host.id` | Identifier for the host machine | `"web-server-01-1234"`, `"worker-node-5678"` |
| `storage.type` | Type of storage backend | `"sqlite"`, `"postgresql"`, `"memory"` |
| `status` | Operation status | `"completed"`, `"failed"` |
| `error.type` | Type of error that occurred | `"ArgumentException"`, `"TimeoutException"` |
| `event.name` | Name of the external event | `"OrderApproved"`, `"PaymentCompleted"` |

## Usage Examples

### Basic Metric Collection

```csharp
// Create metrics instance
var metrics = new DurableFunctionsMetrics();

// Create runtime with metrics enabled
var runtime = new DurableFunctionRuntime(
    stateStore, 
    logger, 
    options: null, 
    loggerFactory: null, 
    metrics: metrics);
```

### OpenTelemetry Integration

```csharp
services.AddOpenTelemetry()
    .WithMetrics(builder => builder
        .AddMeter("Asynkron.DurableFunctions")
        .AddPrometheusExporter()
        .AddOtlpExporter());
```

### Prometheus Configuration

```yaml
scrape_configs:
  - job_name: 'durable-functions'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
    scrape_interval: 15s
```

## Performance Considerations

- Metrics collection has minimal performance overhead (< 2%)
- High-cardinality tags like `instance.id` are limited to active instances
- Metrics are recorded asynchronously to avoid blocking operations
- Consider sampling for high-throughput scenarios

## Troubleshooting

### No Metrics Appearing

1. Verify the `DurableFunctionsMetrics` instance is passed to the runtime
2. Check OpenTelemetry configuration includes the meter name
3. Ensure metrics exporter is properly configured

### High Memory Usage

1. Check for high-cardinality tag values
2. Consider reducing retention period for completed instances
3. Implement metric sampling for high-volume operations

## Related Documentation

- [OpenTelemetry Metrics Guide](https://opentelemetry.io/docs/instrumentation/net/metrics/)
- [Grafana Dashboard Templates](../dashboards/)
- [Prometheus Configuration Examples](../prometheus/)