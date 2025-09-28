using System.Diagnostics;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Minimal sample that pushes Durable Functions tracing data to the default OTLP collector endpoint (localhost:4317).
/// Use together with docker-compose's Jaeger service to inspect spans.
/// </summary>
public static class OpenTelemetryCollectorExample
{
    private static readonly ActivitySource SampleActivitySource = new("DurableFunctions.Demo");

    public static async Task RunAsync(string[] args)
    {
        Console.WriteLine("🔭 OpenTelemetry Collector Example");
        Console.WriteLine("==================================\n");

        var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
        Console.WriteLine($"Using OTLP endpoint: {endpoint}");
        Console.WriteLine("Make sure Jaeger (or any OTLP collector) is listening on that address.\n");

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("durable-functions-example"))
            .AddSource("Asynkron.DurableFunctions")
            .AddSource(SampleActivitySource.Name)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(endpoint);
                options.Protocol = OtlpExportProtocol.Grpc;
            })
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var stateStore = new InMemoryStateStore();
        var runtime = new DurableFunctionRuntime(stateStore, loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        RegisterDemoWorkflow(runtime);

        await EmitStartupDemoSpansAsync();

        var instanceId = $"order-demo-{Guid.NewGuid():N}";
        Console.WriteLine($"Triggering demo orchestration: {instanceId}\n");

        await runtime.TriggerAsync(instanceId, "OrderFulfillmentWorkflow", null);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await runtime.RunAndPollAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected once the orchestration completes and we cancel the run loop
        }

        Console.WriteLine("✅ Orchestration finished. Check your collector to inspect the spans.\n");
        Console.WriteLine("Tip: launch docker-compose with the Jaeger service and browse http://localhost:16686 to view the traces.\n");

        tracerProvider.ForceFlush();
    }

    private static void RegisterDemoWorkflow(DurableFunctionRuntime runtime)
    {
        runtime.RegisterFunction<string, string>("ValidatePayment", async value =>
        {
            await Task.Delay(100);
            return string.IsNullOrEmpty(value) ? "invalid" : value.ToUpperInvariant();
        });

        runtime.RegisterFunction<string, string>("ReserveInventory", async value =>
        {
            await Task.Delay(150);
            return $"processed::{value}";
        });

        runtime.RegisterFunction<string, string>("CreateShippingLabel", async value =>
        {
            await Task.Delay(75);
            return $"saved::{value}";
        });

        runtime.RegisterOrchestrator<string, string>("OrderFulfillmentWorkflow", async (context, input) =>
        {
            var payload = string.IsNullOrEmpty(input) ? "durable" : input;
            var validated = await context.CallAsync<string>("ValidatePayment", payload);
            var reserved = await context.CallAsync<string>("ReserveInventory", validated);
            var shippingLabel = await context.CallAsync<string>("CreateShippingLabel", reserved);
            return shippingLabel;
        });
    }

    private static async Task EmitStartupDemoSpansAsync()
    {
        using var root = SampleActivitySource.StartActivity("demo.startup");
        root?.SetTag("demo.step", "root");

        using (var child = SampleActivitySource.StartActivity("demo.child"))
        {
            child?.SetTag("demo.work", "initializing");
            await Task.Delay(50);

            using var grandChild = SampleActivitySource.StartActivity("demo.grandchild");
            grandChild?.SetTag("demo.work", "nested-operation");
            await Task.Delay(50);
        }
    }
}
