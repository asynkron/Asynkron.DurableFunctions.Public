using System.Text.Json;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Complete ASP.NET Core Web API example using PostgreSQL for durable functions.
/// </summary>
public class WebApiPostgreSqlExample
{
    public static async Task RunAsync(string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());

        builder.Logging.AddConsole();

        ConfigureServices(builder);

        var app = builder.Build();
        ConfigureApplication(app);

        await app.RunAsync();
    }

    private static void ConfigureApplication(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.UseDurableTraceContext();
        app.MapControllers();
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

        var runtime = app.Services.GetRequiredService<DurableFunctionRuntime>();
        RegisterFunctions(runtime);

        var logger = app.Services.GetService<ILogger<WebApiPostgreSqlExample>>();

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var stoppingToken = app.Lifetime.ApplicationStopping;

            _ = Task.Run(async () =>
            {
                try
                {
                    await runtime.RunAndPollAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    logger?.LogInformation("Durable Functions runtime stopping.");
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Durable Functions runtime stopped unexpectedly.");
                }
            });
        });
    }

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Configure PostgreSQL connection
        var connectionString = builder.Configuration.GetConnectionString("PostgreSQL") 
                              ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
                              ?? "Host=localhost;Port=5432;Database=durablefunctions;Username=durableuser;Password=durablepass;Include Error Detail=true;";

        // Add Durable Functions with PostgreSQL
        builder.Services.AddDurableFunctionsWithPostgreSQL(connectionString, options =>
        {
            options.PollingIntervalMs = 100;
        });

        // Add management services with HTTP API
        builder.Services.AddDurableFunctionsManagement(options =>
        {
            options.BaseUrl = builder.Configuration["DurableFunctions:BaseUrl"] ?? "https://localhost:7001";
        });

        // Add standard web services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Durable Functions PostgreSQL API", Version = "v1" });
        });
    }

    public static void RegisterFunctions(DurableFunctionRuntime runtime)
    {
        // Register business logic activities
        runtime.RegisterFunction<OrderProcessingInput, OrderResult>("ProcessOrder", ProcessOrderActivity);
        runtime.RegisterFunction<PaymentInput, PaymentResult>("ProcessPayment", ProcessPaymentActivity);
        runtime.RegisterFunction<InventoryInput, InventoryResult>("ReserveInventory", ReserveInventoryActivity);
        runtime.RegisterFunction<string, EmailResult>("SendEmailNotification", SendEmailActivity);

        // Register the main orchestrator
        runtime.RegisterOrchestrator<OrderProcessingInput, OrderProcessingResult>("OrderProcessingOrchestrator", OrderProcessingOrchestrator);

        // Register a long-running batch process orchestrator
        runtime.RegisterOrchestrator<BatchProcessingInput, BatchResult>("BatchProcessingOrchestrator", BatchProcessingOrchestrator);
    }

    // Business Logic Activities
    private static async Task<OrderResult> ProcessOrderActivity(OrderProcessingInput input)
    {
        // Simulate order validation and processing
        await Task.Delay(500);
        
        return new OrderResult
        {
            OrderId = input.OrderId,
            Status = "Validated",
            ProcessedAt = DateTimeOffset.UtcNow,
            TotalAmount = input.Items.Sum(i => i.Price * i.Quantity)
        };
    }

    private static async Task<PaymentResult> ProcessPaymentActivity(PaymentInput input)
    {
        // Simulate payment processing
        await Task.Delay(1000);
        
        // 10% chance of failure for demo purposes
        if (Random.Shared.Next(100) < 10)
        {
            throw new InvalidOperationException("Payment processing failed - insufficient funds");
        }

        return new PaymentResult
        {
            TransactionId = Guid.NewGuid().ToString(),
            Amount = input.Amount,
            Status = "Completed",
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<InventoryResult> ReserveInventoryActivity(InventoryInput input)
    {
        // Simulate inventory reservation
        await Task.Delay(300);
        
        return new InventoryResult
        {
            ReservationId = Guid.NewGuid().ToString(),
            ItemsReserved = input.Items,
            ReservedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<EmailResult> SendEmailActivity(string message)
    {
        // Simulate email sending
        await Task.Delay(200);
        
        return new EmailResult
        {
            MessageId = Guid.NewGuid().ToString(),
            SentAt = DateTimeOffset.UtcNow,
            Status = "Sent"
        };
    }

    // Main Orchestrator
    private static async Task<OrderProcessingResult> OrderProcessingOrchestrator(IOrchestrationContext context, OrderProcessingInput input)
    {
        var result = new OrderProcessingResult { OrderId = input.OrderId };

        try
        {
            // Step 1: Process the order
            var orderResult = await context.CallAsync<OrderResult>("ProcessOrder", input);
            result.OrderResult = orderResult;

            // Step 2: Reserve inventory
            var inventoryInput = new InventoryInput { Items = input.Items };
            var inventoryResult = await context.CallAsync<InventoryResult>("ReserveInventory", inventoryInput);
            result.InventoryResult = inventoryResult;

            // Step 3: Process payment
            var paymentInput = new PaymentInput 
            { 
                Amount = orderResult.TotalAmount,
                CustomerId = input.CustomerId 
            };
            var paymentResult = await context.CallAsync<PaymentResult>("ProcessPayment", paymentInput);
            result.PaymentResult = paymentResult;

            // Step 4: Send confirmation email
            var emailMessage = $"Order {input.OrderId} has been processed successfully! Total: ${orderResult.TotalAmount:F2}";
            var emailResult = await context.CallAsync<EmailResult>("SendEmailNotification", emailMessage);
            result.EmailResult = emailResult;

            result.Status = "Completed";
            result.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            result.Status = "Failed";
            result.Error = ex.Message;
            result.CompletedAt = DateTimeOffset.UtcNow;

            // Send failure notification
            var failureMessage = $"Order {input.OrderId} failed: {ex.Message}";
            await context.CallAsync<EmailResult>("SendEmailNotification", failureMessage);
        }

        return result;
    }

    // Batch Processing Orchestrator
    private static async Task<BatchResult> BatchProcessingOrchestrator(IOrchestrationContext context, BatchProcessingInput input)
    {
        var tasks = new List<Task<OrderProcessingResult>>();

        // Process orders in parallel batches
        var batchSize = 5;
        for (int i = 0; i < input.Orders.Count; i += batchSize)
        {
            var batch = input.Orders.Skip(i).Take(batchSize);
            
            foreach (var order in batch)
            {
                var task = context.CallAsync<OrderProcessingResult>("OrderProcessingOrchestrator", order);
                tasks.Add(task);
            }

            // Wait for current batch to complete before starting next batch
            await Task.WhenAll(tasks);
        }

        var results = await Task.WhenAll(tasks);
        
        return new BatchResult
        {
            BatchId = input.BatchId,
            TotalOrders = input.Orders.Count,
            SuccessfulOrders = results.Count(r => r.Status == "Completed"),
            FailedOrders = results.Count(r => r.Status == "Failed"),
            ProcessedAt = DateTimeOffset.UtcNow,
            Results = results.ToList()
        };
    }
}

// API Controller
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly DurableFunctionRuntime _runtime;
    private readonly IOrchestrationClient _orchestrationClient;

    public OrdersController(DurableFunctionRuntime runtime, IOrchestrationClient orchestrationClient)
    {
        _runtime = runtime;
        _orchestrationClient = orchestrationClient;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderProcessingInput input)
    {
        var instanceId = Guid.NewGuid().ToString();
        await _runtime.TriggerAsync(instanceId, "OrderProcessingOrchestrator", JsonSerializer.Serialize(input));
        
        return Ok(new { InstanceId = instanceId, Message = "Order processing started" });
    }

    [HttpPost("batch")]
    public async Task<IActionResult> ProcessBatch([FromBody] BatchProcessingInput input)
    {
        var instanceId = Guid.NewGuid().ToString();
        await _runtime.TriggerAsync(instanceId, "BatchProcessingOrchestrator", JsonSerializer.Serialize(input));
        
        return Ok(new { InstanceId = instanceId, Message = "Batch processing started" });
    }

    [HttpGet("{instanceId}")]
    public async Task<IActionResult> GetOrderStatus(string instanceId)
    {
        var status = await _orchestrationClient.GetStatusAsync(instanceId);
        return Ok(status);
    }
}

// Data Models
public class OrderProcessingInput
{
    public string OrderId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class OrderResult
{
    public string OrderId { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public class PaymentInput
{
    public decimal Amount { get; set; }
    public string CustomerId { get; set; } = "";
}

public class PaymentResult
{
    public string TransactionId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; }
}

public class InventoryInput
{
    public List<OrderItem> Items { get; set; } = new();
}

public class InventoryResult
{
    public string ReservationId { get; set; } = "";
    public List<OrderItem> ItemsReserved { get; set; } = new();
    public DateTimeOffset ReservedAt { get; set; }
}

public class EmailResult
{
    public string MessageId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}

public class OrderProcessingResult
{
    public string OrderId { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Error { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public OrderResult? OrderResult { get; set; }
    public PaymentResult? PaymentResult { get; set; }
    public InventoryResult? InventoryResult { get; set; }
    public EmailResult? EmailResult { get; set; }
}

public class BatchProcessingInput
{
    public string BatchId { get; set; } = "";
    public List<OrderProcessingInput> Orders { get; set; } = new();
}

public class BatchResult
{
    public string BatchId { get; set; } = "";
    public int TotalOrders { get; set; }
    public int SuccessfulOrders { get; set; }
    public int FailedOrders { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    public List<OrderProcessingResult> Results { get; set; } = new();
}
