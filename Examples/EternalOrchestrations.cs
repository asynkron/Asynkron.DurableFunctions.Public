using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

/// <summary>
/// Eternal orchestrations example showing long-running monitor patterns
/// </summary>
public class EternalOrchestrationsExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Monitor orchestration that runs forever
        runtime.RegisterOrchestratorFunction<string, string>("MonitorOrchestrator", async context =>
        {
            var monitorConfig = context.GetInput<string>();
            int checkCount = 0;
            
            while (checkCount < 5) // Limited for demo - normally would be while (true)
            {
                checkCount++;
                Console.WriteLine($"🔍 Monitor check #{checkCount}");
                
                // Check system health
                var status = await context.CallFunction<string>("CheckSystemHealth", monitorConfig);
                
                if (status != "OK")
                {
                    await context.CallFunction("SendAlert", status);
                }
                
                // Wait 10 seconds before next check (normally would be minutes/hours)
                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(10));
            }
            
            return $"Monitor completed {checkCount} checks";
        });

        // Periodic cleanup orchestration
        runtime.RegisterOrchestratorFunction<string, string>("PeriodicCleanupOrchestrator", async context =>
        {
            var config = context.GetInput<string>();
            int cleanupCount = 0;
            
            while (cleanupCount < 3) // Limited for demo
            {
                cleanupCount++;
                Console.WriteLine($"🧹 Cleanup cycle #{cleanupCount}");
                
                // Perform cleanup tasks
                await context.CallFunction("CleanupTempFiles", config);
                await context.CallFunction("OptimizeDatabase", config);
                await context.CallFunction("ArchiveOldLogs", config);
                
                // Wait for next cleanup cycle (normally would be daily/weekly)
                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(15));
            }
            
            return $"Cleanup completed {cleanupCount} cycles";
        });

        // Heartbeat orchestration
        runtime.RegisterOrchestratorFunction<string, string>("HeartbeatOrchestrator", async context =>
        {
            var serviceConfig = context.GetInput<string>();
            int heartbeatCount = 0;
            
            while (heartbeatCount < 6) // Limited for demo
            {
                heartbeatCount++;
                Console.WriteLine($"💓 Heartbeat #{heartbeatCount}");
                
                // Send heartbeat
                await context.CallFunction("SendHeartbeat", serviceConfig);
                
                // Wait 5 seconds before next heartbeat
                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(5));
            }
            
            return $"Heartbeat sent {heartbeatCount} times";
        });

        // Register activity functions
        runtime.RegisterFunction<string, string>("CheckSystemHealth", async config =>
        {
            Console.WriteLine($"  🏥 Checking system health for: {config}");
            await Task.Delay(100);
            
            // Simulate occasional issues
            if (Random.Shared.NextDouble() < 0.2)
            {
                return "HIGH_MEMORY_USAGE";
            }
            
            return "OK";
        });

        runtime.RegisterFunction<string, string>("SendAlert", async status =>
        {
            Console.WriteLine($"  🚨 ALERT: System status is {status}");
            await Task.Delay(50);
            return "Alert sent";
        });

        runtime.RegisterFunction<string, string>("CleanupTempFiles", async config =>
        {
            Console.WriteLine($"  🗑️ Cleaning up temp files for: {config}");
            await Task.Delay(200);
            return "Temp files cleaned";
        });

        runtime.RegisterFunction<string, string>("OptimizeDatabase", async config =>
        {
            Console.WriteLine($"  🗃️ Optimizing database for: {config}");
            await Task.Delay(300);
            return "Database optimized";
        });

        runtime.RegisterFunction<string, string>("ArchiveOldLogs", async config =>
        {
            Console.WriteLine($"  📦 Archiving old logs for: {config}");
            await Task.Delay(150);
            return "Logs archived";
        });

        runtime.RegisterFunction<string, string>("SendHeartbeat", async config =>
        {
            Console.WriteLine($"  📡 Sending heartbeat for: {config}");
            await Task.Delay(50);
            return "Heartbeat sent";
        });

        // Run the examples
        Console.WriteLine("Starting eternal orchestration examples...");
        
        await runtime.TriggerAsync("monitor-001", "MonitorOrchestrator", "production-system");
        await runtime.TriggerAsync("cleanup-001", "PeriodicCleanupOrchestrator", "maintenance-config");
        await runtime.TriggerAsync("heartbeat-001", "HeartbeatOrchestrator", "service-001");
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await runtime.RunAndPollAsync(cts.Token);
    }
}