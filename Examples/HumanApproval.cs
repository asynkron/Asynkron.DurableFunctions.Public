using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

namespace Examples;

public class ApprovalRequest
{
    public string Id { get; set; } = "";
    public string RequestType { get; set; } = "";
    public string Requester { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>
/// Human approval workflow example showing external event handling
/// </summary>
public class HumanApprovalExample
{
    public static async Task Run()
    {
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var runtime = new DurableFunctionRuntime(
            stateStore,
            loggerFactory.CreateLogger<DurableFunctionRuntime>(),
            loggerFactory: loggerFactory);

        // Register approval orchestrator
        runtime.RegisterOrchestratorFunction<ApprovalRequest, string>("ApprovalWorkflow", async context =>
        {
            var request = context.GetInput<ApprovalRequest>();
            
            // Submit for approval
            await context.CallFunction("SendApprovalRequest", request);
            
            // Wait for external approval event (could be hours or days!)
            var approvalResult = await context.WaitForExternalEvent<bool>("ApprovalEvent");
            
            if (approvalResult)
            {
                await context.CallFunction("ProcessApprovedRequest", request);
                return "Request approved and processed!";
            }
            else
            {
                await context.CallFunction("HandleRejection", request);
                return "Request was rejected.";
            }
        });

        // Register another approval pattern with timeout
        runtime.RegisterOrchestratorFunction<ApprovalRequest, string>("HumanApprovalOrchestrator", async context =>
        {
            var request = context.GetInput<ApprovalRequest>();
            
            // Send approval request to human
            await context.CallFunction("SendApprovalEmail", request);
            
            // Wait for human response (timeout after 30 seconds for demo - normally 24 hours)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                var approved = await context.WaitForExternalEvent<bool>("HumanApproval");
                return approved ? "Approved by human" : "Rejected by human";
            }
            catch (OperationCanceledException)
            {
                return "Approval timed out";
            }
        });

        // Register activity functions
        runtime.RegisterFunction<ApprovalRequest, string>("SendApprovalRequest", async request =>
        {
            Console.WriteLine($"📧 Sending approval request for {request.RequestType} (${request.Amount}) to approver...");
            await Task.Delay(100);
            return "Approval request sent";
        });

        runtime.RegisterFunction<ApprovalRequest, string>("SendApprovalEmail", async request =>
        {
            Console.WriteLine($"📧 Sending approval email for {request.RequestType} to manager...");
            await Task.Delay(100);
            return "Approval email sent";
        });

        runtime.RegisterFunction<ApprovalRequest, string>("ProcessApprovedRequest", async request =>
        {
            Console.WriteLine($"✅ Processing approved request {request.Id}...");
            await Task.Delay(200);
            return "Request processed";
        });

        runtime.RegisterFunction<ApprovalRequest, string>("HandleRejection", async request =>
        {
            Console.WriteLine($"❌ Handling rejection for request {request.Id}...");
            await Task.Delay(100);
            return "Rejection handled";
        });

        // Run the example
        var sampleRequest = new ApprovalRequest
        {
            Id = "REQ-001",
            RequestType = "Budget Increase",
            Requester = "john.doe@example.com",
            Amount = 5000m
        };

        Console.WriteLine("Starting approval workflow...");
        await runtime.TriggerAsync("approval-001", "ApprovalWorkflow", sampleRequest);

        // Simulate async approval after 10 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            Console.WriteLine("🎯 Simulating approval event...");
            await runtime.RaiseEventAsync("approval-001", "ApprovalEvent", true);
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await runtime.RunAndPollAsync(cts.Token);
    }
}