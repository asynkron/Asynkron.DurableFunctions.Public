using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

/// <summary>
/// Durable timers example showing long-running workflows with delays
/// </summary>
public class DurableTimersExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Register orchestrator with timers
        runtime.RegisterOrchestratorFunction<string, string>("LongRunningProcess", async context =>
        {
            var customerEmail = context.GetInput<string>();
            var startTime = context.CurrentUtcDateTime;
            
            // Send welcome email immediately
            await context.CallFunction("SendWelcomeEmail", customerEmail);
            
            // Wait 5 seconds (shortened for demo - normally would be 24 hours)
            var followUpTime = startTime.AddSeconds(5);
            await context.CreateTimer(followUpTime);
            
            // Send follow-up email after delay
            await context.CallFunction("SendFollowUpEmail", customerEmail);
            
            // Wait another 10 seconds (normally would be a whole week)
            var newsletterTime = startTime.AddSeconds(15);
            await context.CreateTimer(newsletterTime);
            
            // Send weekly newsletter
            await context.CallFunction("SendWeeklyNewsletter", customerEmail);
            
            return "Email sequence completed over time!";
        });

        // Register a simple timer example
        runtime.RegisterOrchestratorFunction<string, string>("DelayedGreeting", async context =>
        {
            var name = context.GetInput<string>();
            
            Console.WriteLine($"⏰ Setting timer for 5 seconds...");
            var dueTime = context.CurrentUtcDateTime.AddSeconds(5);
            await context.CreateTimer(dueTime);
            
            Console.WriteLine($"🎉 Timer fired! Greeting {name}");
            return $"Hello {name} (after delay)!";
        });

        // Register activity functions
        runtime.RegisterFunction<string, string>("SendWelcomeEmail", async email =>
        {
            Console.WriteLine($"📧 Sending welcome email to {email}");
            await Task.Delay(100);
            return "Welcome email sent";
        });

        runtime.RegisterFunction<string, string>("SendFollowUpEmail", async email =>
        {
            Console.WriteLine($"📧 Sending follow-up email to {email}");
            await Task.Delay(100);
            return "Follow-up email sent";
        });

        runtime.RegisterFunction<string, string>("SendWeeklyNewsletter", async email =>
        {
            Console.WriteLine($"📧 Sending weekly newsletter to {email}");
            await Task.Delay(100);
            return "Newsletter sent";
        });

        // Run the examples
        Console.WriteLine("Starting timer examples...");
        
        await runtime.TriggerAsync("timer-001", "DelayedGreeting", "World");
        await runtime.TriggerAsync("email-001", "LongRunningProcess", "customer@example.com");
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await runtime.RunAndPollAsync(cts.Token);
    }
}