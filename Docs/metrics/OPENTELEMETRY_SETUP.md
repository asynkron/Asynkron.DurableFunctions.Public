# OpenTelemetry Setup for Durable Functions Metrics

This guide shows how to configure OpenTelemetry to collect and export Durable Functions metrics.

## Prerequisites

Install the required OpenTelemetry packages:

```xml
<PackageReference Include="OpenTelemetry" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.6.0-rc.1" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.7.0" />
```

## Basic Setup

### 1. ASP.NET Core Integration

```csharp
// Program.cs
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Monitoring;
using Asynkron.DurableFunctions.Persistence;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Register Durable Functions services
builder.Services.AddSingleton<IStateStore, SqliteStateStore>();
builder.Services.AddSingleton<DurableFunctionsMetrics>();
builder.Services.AddSingleton<DurableFunctionRuntime>(serviceProvider =>
{
    var stateStore = serviceProvider.GetRequiredService<IStateStore>();
    var logger = serviceProvider.GetRequiredService<ILogger<DurableFunctionRuntime>>();
    var metrics = serviceProvider.GetRequiredService<DurableFunctionsMetrics>();
    
    return new DurableFunctionRuntime(stateStore, logger, metrics: metrics);
});

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(builder => builder
        .AddMeter("Asynkron.DurableFunctions")
        .AddPrometheusExporter()
        .AddOtlpExporter()
        .AddConsoleExporter());

var app = builder.Build();

// Enable Prometheus metrics endpoint
app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();
```

### 2. Console Application

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Monitoring;
using Asynkron.DurableFunctions.Persistence;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register Durable Functions services
        services.AddSingleton<IStateStore, SqliteStateStore>();
        services.AddSingleton<DurableFunctionsMetrics>();
        services.AddSingleton<DurableFunctionRuntime>(serviceProvider =>
        {
            var stateStore = serviceProvider.GetRequiredService<IStateStore>();
            var logger = serviceProvider.GetRequiredService<ILogger<DurableFunctionRuntime>>();
            var metrics = serviceProvider.GetRequiredService<DurableFunctionsMetrics>();
            
            return new DurableFunctionRuntime(stateStore, logger, metrics: metrics);
        });

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .WithMetrics(builder => builder
                .AddMeter("Asynkron.DurableFunctions")
                .AddConsoleExporter()
                .AddOtlpExporter());
    })
    .Build();

await host.RunAsync();
```

## Advanced Configuration

### 1. Prometheus with Custom Endpoint

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(builder => builder
        .AddMeter("Asynkron.DurableFunctions")
        .AddPrometheusExporter(options =>
        {
            options.HttpListenerPrefixes = new[] { "http://localhost:9090/" };
        }));
```

### 2. OTLP with Authentication

```csharp
builder.Services.Configure<OtlpExporterOptions>(options =>
{
    options.Endpoint = new Uri("https://your-otlp-endpoint");
    options.Headers = "Authorization=Bearer your-token";
    options.Protocol = OtlpExportProtocol.HttpProtobuf;
});
```

### 3. Selective Metric Collection

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(builder => builder
        .AddMeter("Asynkron.DurableFunctions")
        .AddView("orchestrations.duration", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[] { 0.1, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0 }
        })
        .AddView("state.*.duration", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = new[] { 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0 }
        }));
```

## Configuration Options

### Environment Variables

```bash
# OTLP Exporter
export OTEL_EXPORTER_OTLP_ENDPOINT="https://your-otlp-endpoint"
export OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer your-token"
export OTEL_EXPORTER_OTLP_PROTOCOL="http/protobuf"

# Prometheus Exporter
export OTEL_PROMETHEUS_PORT="9090"
export OTEL_PROMETHEUS_HOST="0.0.0.0"

# Console Exporter
export OTEL_DOTNET_EXPERIMENTAL_CONSOLE_EXPORTER_ENABLED="true"
```

### appsettings.json

```json
{
  "OpenTelemetry": {
    "Metrics": {
      "Exporters": {
        "Prometheus": {
          "Port": 9090,
          "Path": "/metrics"
        },
        "Otlp": {
          "Endpoint": "https://your-otlp-endpoint",
          "Protocol": "HttpProtobuf"
        }
      }
    }
  }
}
```

## Grafana Integration

### 1. Prometheus Data Source

```yaml
# prometheus.yml
global:
  scrape_interval: 15s

rule_files:
  - "durable_functions_rules.yml"

scrape_configs:
  - job_name: 'durable-functions'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
    scrape_interval: 15s
    metrics_path: '/metrics'
```

### 2. Sample Grafana Queries

```promql
# Orchestration success rate
rate(orchestrations_completed_total[5m]) / rate(orchestrations_started_total[5m]) * 100

# Average orchestration duration
histogram_quantile(0.95, rate(orchestrations_duration_seconds_bucket[5m]))

# Active orchestrations by host
sum by (host_id) (orchestrations_active)

# Function failure rate
rate(functions_failures_total[5m]) / rate(functions_calls_total[5m]) * 100

# State store latency
histogram_quantile(0.95, rate(state_save_duration_seconds_bucket[5m]))
```

## Docker Compose Example

```yaml
version: '3.8'
services:
  app:
    build: .
    ports:
      - "5000:5000"
      - "9090:9090"  # Prometheus metrics
    environment:
      - OTEL_EXPORTER_PROMETHEUS_PORT=9090
    
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9091:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/etc/prometheus/console_libraries'
      - '--web.console.templates=/etc/prometheus/consoles'
    
  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-storage:/var/lib/grafana

volumes:
  grafana-storage:
```

## Troubleshooting

### Common Issues

1. **Metrics not appearing in Prometheus**
   - Verify the metrics endpoint is accessible: `curl http://localhost:9090/metrics`
   - Check that the meter name "Asynkron.DurableFunctions" is registered
   - Ensure operations are actually triggering metrics

2. **High cardinality warnings**
   - Limit the use of high-cardinality tags like `instance.id`
   - Consider sampling for high-volume operations
   - Use metric aggregation at collection time

3. **Performance impact**
   - Monitor application performance after enabling metrics
   - Use sampling for high-frequency operations
   - Consider disabling detailed instance-level metrics in production

### Debugging

Enable debug logging for OpenTelemetry:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Add this to see OpenTelemetry internal logs
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
```

## Best Practices

1. **Use appropriate metric types**
   - Counters for cumulative values (total requests)
   - Histograms for measuring distributions (duration, size)
   - Gauges for current values (active connections)

2. **Optimize cardinality**
   - Avoid unbounded tag values
   - Group related metrics with consistent tags
   - Use sampling for high-volume metrics

3. **Configure appropriate retention**
   - Set proper bucket boundaries for histograms
   - Configure metric retention based on your needs
   - Use aggregation to reduce storage requirements

4. **Monitor the monitoring**
   - Track exporter success/failure rates
   - Monitor metric collection overhead
   - Set up alerts for metric collection failures