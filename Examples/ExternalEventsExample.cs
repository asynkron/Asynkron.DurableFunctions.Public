using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating external events functionality for human approval workflows.
/// Shows how orchestrations can pause and wait for external events, then resume when events arrive.
/// </summary>
public class ExternalEventsExample
{
    public static async Task RunExample()
    {
        // Setup
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);

        // Register human approval orchestrator
        runtime.RegisterOrchestrator<ApprovalRequest, string>("HumanApprovalOrchestrator", async (context, request) =>
        {
            Console.WriteLine($"[Orchestrator] Starting approval process for: {request.Title}");

            // Step 1: Send approval request to human
            await context.CallAsync("SendApprovalEmail", request);

            // Step 2: Wait for human approval (this will pause the orchestrator)
            Console.WriteLine($"[Orchestrator] Waiting for human approval...");
            var approvalResponse = await context.WaitForEvent<ApprovalResponse>("ApprovalDecision");

            // Step 3: Process the approval decision
            Console.WriteLine($"[Orchestrator] Received approval decision: {approvalResponse.Decision}");

            if (approvalResponse.Approved)
            {
                await context.CallAsync("ProcessApprovedRequest", request);
                return
                    $"✅ Request '{request.Title}' was approved by {approvalResponse.ApproverName}. Reason: {approvalResponse.Comments}";
            }
            else
            {
                await context.CallAsync("ProcessRejectedRequest", request);
                return
                    $"❌ Request '{request.Title}' was rejected by {approvalResponse.ApproverName}. Reason: {approvalResponse.Comments}";
            }
        });

        // Register timeout orchestrator that shows event queuing
        runtime.RegisterOrchestrator<string, string>("MultiEventOrchestrator", async (context, input) =>
        {
            Console.WriteLine($"[Multi-Event] Starting orchestrator with input: {input}");

            // Wait for multiple events in sequence to demonstrate queuing
            Console.WriteLine("[Multi-Event] Waiting for first event...");
            var event1 = await context.WaitForEvent<string>("StatusUpdate");
            Console.WriteLine($"[Multi-Event] Received first event: {event1}");

            Console.WriteLine("[Multi-Event] Waiting for second event...");
            var event2 = await context.WaitForEvent<string>("StatusUpdate");
            Console.WriteLine($"[Multi-Event] Received second event: {event2}");

            Console.WriteLine("[Multi-Event] Waiting for completion event...");
            var finalEvent = await context.WaitForEvent<string>("CompletionEvent");
            Console.WriteLine($"[Multi-Event] Received final event: {finalEvent}");

            return $"Multi-event workflow completed: {event1} -> {event2} -> {finalEvent}";
        });

        // Register mock activities
        runtime.RegisterFunction<ApprovalRequest, string>("SendApprovalEmail", async request =>
        {
            await Task.Delay(100);
            Console.WriteLine($"📧 [Activity] Approval email sent for: {request.Title}");
            Console.WriteLine($"   📋 Description: {request.Description}");
            Console.WriteLine($"   💰 Amount: ${request.Amount:N2}");
            Console.WriteLine($"   👤 Requested by: {request.RequestedBy}");
            return "Email sent successfully";
        });

        runtime.RegisterFunction<ApprovalRequest, string>("ProcessApprovedRequest", async request =>
        {
            await Task.Delay(200);
            Console.WriteLine($"✅ [Activity] Processing approved request: {request.Title}");
            return "Request processed successfully";
        });

        runtime.RegisterFunction<ApprovalRequest, string>("ProcessRejectedRequest", async request =>
        {
            await Task.Delay(100);
            Console.WriteLine($"❌ [Activity] Handling rejected request: {request.Title}");
            return "Rejection processed";
        });

        Console.WriteLine("=== External Events Example ===");
        Console.WriteLine("Demonstrating human approval workflow with external events");
        Console.WriteLine();

        // Create sample approval request
        var approvalRequest = new ApprovalRequest
        {
            Id = "REQ-2023-001",
            Title = "New Marketing Campaign Budget",
            Description = "Requesting budget approval for Q4 marketing campaign targeting enterprise customers",
            Amount = 50000.00m,
            RequestedBy = "Sarah Johnson",
            Department = "Marketing"
        };

        // Trigger the approval orchestrator
        await runtime.TriggerAsyncObject("approval-example", "HumanApprovalOrchestrator", approvalRequest);

        // Start the runtime in background
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var runtimeTask = runtime.RunAndPollAsync(cts.Token);

        // Simulate human delay - wait a moment before approving
        Console.WriteLine("⏳ Simulating human thinking time (2 seconds)...");
        await Task.Delay(2000);

        // Get the actual instance ID for raising the event
        var states = await stateStore.GetReadyStatesAsync(DateTimeOffset.UtcNow.AddMinutes(1));
        var orchestratorInstance = states.FirstOrDefault(s => s.FunctionName == "HumanApprovalOrchestrator");

        if (orchestratorInstance != null)
        {
            // Simulate human approval
            var approvalResponse = new ApprovalResponse
            {
                Approved = true,
                Decision = "Approved",
                ApproverName = "Michael Smith",
                Comments = "Budget is reasonable and campaign strategy is solid. Approved for Q4 execution."
            };

            Console.WriteLine($"👤 [Human] Manager decides: {approvalResponse.Decision}");
            Console.WriteLine($"💭 [Human] Comments: {approvalResponse.Comments}");

            // Raise the external event
            await runtime.RaiseEventAsync(orchestratorInstance.InstanceId, "ApprovalDecision", approvalResponse);
        }

        // Let the workflow complete
        await Task.Delay(1000);

        Console.WriteLine();
        Console.WriteLine("=== Multi-Event Example ===");
        Console.WriteLine("Demonstrating multiple external events and queuing");

        // Start multi-event orchestrator
        await runtime.TriggerAsyncObject("multi-event-example", "MultiEventOrchestrator", "Process-XYZ");

        // Wait a moment for it to start
        await Task.Delay(500);

        // Get the multi-event orchestrator instance
        var multiEventStates = await stateStore.GetReadyStatesAsync(DateTimeOffset.UtcNow.AddMinutes(1));
        var multiEventInstance = multiEventStates.FirstOrDefault(s => s.FunctionName == "MultiEventOrchestrator");

        if (multiEventInstance != null)
        {
            // Send multiple events with some delay
            Console.WriteLine("📡 Sending status updates...");

            await Task.Delay(500);
            await runtime.RaiseEventAsync(multiEventInstance.InstanceId, "StatusUpdate", "Phase 1 Complete");

            await Task.Delay(500);
            await runtime.RaiseEventAsync(multiEventInstance.InstanceId, "StatusUpdate", "Phase 2 Complete");

            await Task.Delay(500);
            await runtime.RaiseEventAsync(multiEventInstance.InstanceId, "CompletionEvent", "All Phases Complete");
        }

        // Wait for completion
        try
        {
            await runtimeTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Runtime stopped.");
        }

        Console.WriteLine();
        Console.WriteLine("=== External Events Example Completed ===");
    }
}

public class ApprovalRequest
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string RequestedBy { get; set; } = "";
    public string Department { get; set; } = "";
}

public class ApprovalResponse
{
    public bool Approved { get; set; }
    public string Decision { get; set; } = "";
    public string ApproverName { get; set; } = "";
    public string Comments { get; set; } = "";
}