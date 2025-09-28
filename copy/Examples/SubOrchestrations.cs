using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

/// <summary>
/// Sub-orchestration example showing orchestrator composition
/// </summary>
public class SubOrchestrationsExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Child orchestrator for processing individual orders
        runtime.RegisterOrchestratorFunction<string, string>("ProcessOrderOrchestrator", async context =>
        {
            var orderId = context.GetInput<string>();
            Console.WriteLine($"🔄 Processing order: {orderId}");
            
            await context.CallFunction("ValidateOrder", orderId);
            await context.CallFunction("ChargePayment", orderId);
            await context.CallFunction("FulfillOrder", orderId);
            
            return $"Order {orderId} processed";
        });

        // Parent orchestrator calling sub-orchestrators
        runtime.RegisterOrchestratorFunction<string[], string>("MainOrchestrator", async context =>
        {
            var orderIds = context.GetInput<string[]>();
            Console.WriteLine($"📦 Processing {orderIds.Length} orders...");
            
            // Process each order using sub-orchestrators
            var tasks = orderIds.Select(orderId =>
                context.CallSubOrchestratorAsync<string>("ProcessOrderOrchestrator", orderId)
            ).ToArray();
            
            var results = await Task.WhenAll(tasks);
            
            await context.CallFunction("SendBatchNotification", results);
            return $"Batch processing completed: {results.Length} orders processed";
        });

        // Complex example with nested sub-orchestrations
        runtime.RegisterOrchestratorFunction<string, string>("BusinessProcessOrchestrator", async context =>
        {
            var processId = context.GetInput<string>();
            
            // Step 1: Data preparation (sub-orchestration)
            var preparedData = await context.CallSubOrchestratorAsync<string>("DataPreparationOrchestrator", processId);
            
            // Step 2: Business logic (sub-orchestration)
            var processedData = await context.CallSubOrchestratorAsync<string>("BusinessLogicOrchestrator", preparedData);
            
            // Step 3: Finalization (sub-orchestration)
            var finalResult = await context.CallSubOrchestratorAsync<string>("FinalizationOrchestrator", processedData);
            
            return finalResult;
        });

        // Sub-orchestrators for the complex example
        runtime.RegisterOrchestratorFunction<string, string>("DataPreparationOrchestrator", async context =>
        {
            var data = context.GetInput<string>();
            Console.WriteLine($"📊 Preparing data: {data}");
            
            await context.CallFunction("LoadData", data);
            await context.CallFunction("CleanData", data);
            await context.CallFunction("ValidateData", data);
            
            return $"prepared-{data}";
        });

        runtime.RegisterOrchestratorFunction<string, string>("BusinessLogicOrchestrator", async context =>
        {
            var data = context.GetInput<string>();
            Console.WriteLine($"🧠 Processing business logic: {data}");
            
            await context.CallFunction("ApplyBusinessRules", data);
            await context.CallFunction("CalculateResults", data);
            
            return $"processed-{data}";
        });

        runtime.RegisterOrchestratorFunction<string, string>("FinalizationOrchestrator", async context =>
        {
            var data = context.GetInput<string>();
            Console.WriteLine($"🏁 Finalizing: {data}");
            
            await context.CallFunction("GenerateReport", data);
            await context.CallFunction("SendNotification", data);
            
            return $"completed-{data}";
        });

        // Register activity functions
        runtime.RegisterFunction<string, string>("ValidateOrder", async orderId =>
        {
            Console.WriteLine($"  ✅ Validating {orderId}");
            await Task.Delay(100);
            return "validated";
        });

        runtime.RegisterFunction<string, string>("ChargePayment", async orderId =>
        {
            Console.WriteLine($"  💳 Charging payment for {orderId}");
            await Task.Delay(150);
            return "charged";
        });

        runtime.RegisterFunction<string, string>("FulfillOrder", async orderId =>
        {
            Console.WriteLine($"  📦 Fulfilling {orderId}");
            await Task.Delay(200);
            return "fulfilled";
        });

        runtime.RegisterFunction<string[], string>("SendBatchNotification", async results =>
        {
            Console.WriteLine($"📧 Sending batch notification for {results.Length} results");
            await Task.Delay(100);
            return "batch notification sent";
        });

        // Data preparation functions
        runtime.RegisterFunction<string, string>("LoadData", async data =>
        {
            Console.WriteLine($"  📁 Loading data: {data}");
            await Task.Delay(100);
            return "loaded";
        });

        runtime.RegisterFunction<string, string>("CleanData", async data =>
        {
            Console.WriteLine($"  🧹 Cleaning data: {data}");
            await Task.Delay(80);
            return "cleaned";
        });

        runtime.RegisterFunction<string, string>("ValidateData", async data =>
        {
            Console.WriteLine($"  ✅ Validating data: {data}");
            await Task.Delay(60);
            return "validated";
        });

        // Business logic functions
        runtime.RegisterFunction<string, string>("ApplyBusinessRules", async data =>
        {
            Console.WriteLine($"  📋 Applying business rules: {data}");
            await Task.Delay(120);
            return "rules applied";
        });

        runtime.RegisterFunction<string, string>("CalculateResults", async data =>
        {
            Console.WriteLine($"  🧮 Calculating results: {data}");
            await Task.Delay(100);
            return "calculated";
        });

        // Finalization functions
        runtime.RegisterFunction<string, string>("GenerateReport", async data =>
        {
            Console.WriteLine($"  📄 Generating report: {data}");
            await Task.Delay(150);
            return "report generated";
        });

        runtime.RegisterFunction<string, string>("SendNotification", async data =>
        {
            Console.WriteLine($"  📧 Sending notification: {data}");
            await Task.Delay(80);
            return "notification sent";
        });

        // Run the examples
        Console.WriteLine("Starting sub-orchestration examples...");
        
        var orders = new[] { "ORD-001", "ORD-002", "ORD-003" };
        await runtime.TriggerAsync("batch-001", "MainOrchestrator", orders);
        
        await runtime.TriggerAsync("business-001", "BusinessProcessOrchestrator", "DATA-001");
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await runtime.RunAndPollAsync(cts.Token);
    }
}