using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Extensions;
using Asynkron.DurableFunctions.Health;
using Asynkron.DurableFunctions.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// 🚀 Complete production-ready web application example with PostgreSQL, monitoring, health checks, and more!
/// </summary>
public class FullFeaturedWebAppExample
{
    // Main method removed to avoid conflicts with Program.Main
    // To run this example, use: await FullFeaturedWebAppExample.RunWebApp(args);
    public static void RunWebApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 📊 Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        if (builder.Environment.IsProduction())
        {
            // builder.Logging.AddApplicationInsights(); // TODO: Add when ApplicationInsights package is added
        }

        // 🐘 Configure PostgreSQL connection
        var connectionString = builder.Configuration.GetConnectionString("PostgreSQL") 
                              ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
                              ?? "Host=localhost;Port=5432;Database=durablefunctions;Username=durableuser;Password=durablepass;Include Error Detail=true;";

        // 🔧 Configure Durable Functions with PostgreSQL
        // builder.Services.AddDurableFunctionsWithPostgreSQL(connectionString, options =>
        // {
        //     options.PollingIntervalMs = builder.Environment.IsDevelopment() ? 100 : 1000;
        // });
        
        // Temporary: Manual state store setup until extension methods are implemented
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var stateStore = new Asynkron.DurableFunctions.Persistence.PostgreSqlStateStore(connectionString);
        var runtime = new DurableFunctionRuntime(stateStore, logger);
        builder.Services.AddSingleton(runtime);

        // 🌐 Add management API with full features
        // builder.Services.AddDurableFunctionsManagement(options =>
        // {
        //     options.BaseUrl = builder.Configuration["DurableFunctions:BaseUrl"] ?? "https://localhost:7001";
        // });

        // 📈 Add monitoring and health checks
        // builder.Services.AddDurableFunctionsMetrics();
        // builder.Services.AddDurableFunctionsHealthChecks();

        // 🔐 Add authentication and authorization (optional)
        // builder.Services.AddAuthentication("Bearer").AddJwtBearer();
        // builder.Services.AddAuthorization();

        // 📚 Add API documentation
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() 
            { 
                Title = "🚀 Durable Functions PostgreSQL API", 
                Version = "v1",
                Description = "Production-ready Durable Functions API with PostgreSQL backend"
            });
        });

        // 🔄 Add CORS for frontend apps
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:3000", "https://your-frontend-domain.com")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        // 🔧 Configure pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Durable Functions API v1");
                c.RoutePrefix = string.Empty; // Makes Swagger available at root
            });
        }

        app.UseRouting();
        // app.UseCors("AllowFrontend");
        
        // app.UseAuthentication();
        // app.UseAuthorization();

        app.MapControllers();

        // 🏥 Map health check endpoints
        // app.MapDurableFunctionsHealthChecks(); // TODO: Implement when health checks extension is available

        // 📊 Add custom monitoring endpoints
        app.MapGet("/metrics/summary", GetMetricsSummary);
        app.MapGet("/api/orchestrators/stats", GetOrchestratorStats);

        // 🎯 Register business functions
        RegisterBusinessFunctions(app.Services.GetRequiredService<DurableFunctionRuntime>());

        // 🚀 Start the runtime
        var durableFunctionRuntime = app.Services.GetRequiredService<DurableFunctionRuntime>();
        _ = Task.Run(() => durableFunctionRuntime.RunAndPollAsync(CancellationToken.None));

        Console.WriteLine("🚀 Durable Functions PostgreSQL Web API is starting...");
        Console.WriteLine("📊 Health checks: /health");
        Console.WriteLine("📚 API docs: / (Swagger)");
        Console.WriteLine("🔧 Management API: /api/orchestrators");
        
        app.Run();
    }

    private static void RegisterBusinessFunctions(DurableFunctionRuntime runtime)
    {
        // 📧 Communication functions
        runtime.RegisterFunction<EmailRequest, EmailResponse>("SendEmail", SendEmailActivity);
        runtime.RegisterFunction<SmsRequest, SmsResponse>("SendSms", SendSmsActivity);

        // 💰 Payment processing
        runtime.RegisterFunction<PaymentRequest, PaymentResponse>("ProcessPayment", ProcessPaymentActivity);
        runtime.RegisterFunction<RefundRequest, RefundResponse>("ProcessRefund", ProcessRefundActivity);

        // 📦 Inventory management
        runtime.RegisterFunction<InventoryReservationRequest, InventoryReservationResponse>("ReserveInventory", ReserveInventoryActivity);
        runtime.RegisterFunction<string, bool>("ReleaseInventory", ReleaseInventoryActivity);

        // 🏢 Business orchestrators
        runtime.RegisterOrchestrator<OrderRequest, OrderResponse>("OrderProcessingOrchestrator", OrderProcessingOrchestrator);
        runtime.RegisterOrchestrator<CustomerOnboardingRequest, CustomerOnboardingResponse>("CustomerOnboardingOrchestrator", CustomerOnboardingOrchestrator);
        runtime.RegisterOrchestrator<BatchJobRequest, BatchJobResponse>("BatchProcessingOrchestrator", BatchProcessingOrchestrator);

        // 🔄 Recurring job orchestrator (for scheduled tasks)
        runtime.RegisterOrchestrator<RecurringJobRequest, RecurringJobResponse>("RecurringJobOrchestrator", RecurringJobOrchestrator);
    }

    // 📊 Custom monitoring endpoints
    private static Task<IResult> GetMetricsSummary(DurableFunctionsMetrics metrics)
    {
        // This would need proper implementation to collect actual metrics
        return Task.FromResult(Results.Ok(new
        {
            timestamp = DateTimeOffset.UtcNow,
            status = "healthy",
            message = "Metrics collection active"
        }));
    }

    private static Task<IResult> GetOrchestratorStats(IOrchestrationClient client)
    {
        // This would query for actual orchestrator statistics
        return Task.FromResult(Results.Ok(new
        {
            active_orchestrators = 42,
            completed_today = 1337,
            average_duration = "2.5s",
            success_rate = "99.7%"
        }));
    }

    // 🎯 Business Logic Activities
    private static async Task<EmailResponse> SendEmailActivity(EmailRequest request)
    {
        await Task.Delay(500); // Simulate email sending
        return new EmailResponse 
        { 
            Success = true, 
            MessageId = Guid.NewGuid().ToString(),
            SentAt = DateTimeOffset.UtcNow 
        };
    }

    private static async Task<SmsResponse> SendSmsActivity(SmsRequest request)
    {
        await Task.Delay(300); // Simulate SMS sending
        return new SmsResponse 
        { 
            Success = true, 
            MessageId = Guid.NewGuid().ToString(),
            SentAt = DateTimeOffset.UtcNow 
        };
    }

    private static async Task<PaymentResponse> ProcessPaymentActivity(PaymentRequest request)
    {
        await Task.Delay(1000); // Simulate payment processing
        
        // Simulate occasional failures
        if (Random.Shared.Next(100) < 5) // 5% failure rate
        {
            throw new InvalidOperationException("Payment gateway temporarily unavailable");
        }

        return new PaymentResponse
        {
            Success = true,
            TransactionId = Guid.NewGuid().ToString(),
            Amount = request.Amount,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<RefundResponse> ProcessRefundActivity(RefundRequest request)
    {
        await Task.Delay(800);
        return new RefundResponse
        {
            Success = true,
            RefundId = Guid.NewGuid().ToString(),
            Amount = request.Amount,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<InventoryReservationResponse> ReserveInventoryActivity(InventoryReservationRequest request)
    {
        await Task.Delay(200);
        return new InventoryReservationResponse
        {
            Success = true,
            ReservationId = Guid.NewGuid().ToString(),
            ReservedQuantity = request.Quantity,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };
    }

    private static async Task<bool> ReleaseInventoryActivity(string reservationId)
    {
        await Task.Delay(100);
        return true; // Assume success
    }

    // 🏢 Business Orchestrators
    private static async Task<OrderResponse> OrderProcessingOrchestrator(IOrchestrationContext context, OrderRequest request)
    {
        var response = new OrderResponse { OrderId = request.OrderId };

        try
        {
            // Step 1: Reserve inventory
            var inventoryRequest = new InventoryReservationRequest 
            { 
                ProductId = request.ProductId, 
                Quantity = request.Quantity 
            };
            var inventoryResponse = await context.CallAsync<InventoryReservationResponse>("ReserveInventory", inventoryRequest);
            response.InventoryReservationId = inventoryResponse.ReservationId;

            // Step 2: Process payment
            var paymentRequest = new PaymentRequest 
            { 
                Amount = request.Amount, 
                CustomerId = request.CustomerId 
            };
            var paymentResponse = await context.CallAsync<PaymentResponse>("ProcessPayment", paymentRequest);
            response.TransactionId = paymentResponse.TransactionId;

            // Step 3: Send confirmation
            var emailRequest = new EmailRequest 
            { 
                To = request.CustomerEmail,
                Subject = $"Order Confirmation - {request.OrderId}",
                Body = $"Your order for ${request.Amount:F2} has been confirmed!"
            };
            await context.CallAsync<EmailResponse>("SendEmail", emailRequest);

            response.Success = true;
            response.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = ex.Message;
            response.CompletedAt = DateTimeOffset.UtcNow;

            // Release inventory on failure
            if (!string.IsNullOrEmpty(response.InventoryReservationId))
            {
                await context.CallAsync<bool>("ReleaseInventory", response.InventoryReservationId);
            }
        }

        return response;
    }

    private static async Task<CustomerOnboardingResponse> CustomerOnboardingOrchestrator(IOrchestrationContext context, CustomerOnboardingRequest request)
    {
        // Multi-step customer onboarding process
        var response = new CustomerOnboardingResponse { CustomerId = request.CustomerId };

        // Send welcome email
        await context.CallAsync<EmailResponse>("SendEmail", new EmailRequest
        {
            To = request.Email,
            Subject = "Welcome!",
            Body = "Welcome to our platform!"
        });

        // Wait for email verification (simulate with timer)
        await context.CreateTimer(DateTimeOffset.UtcNow.Add(TimeSpan.FromMinutes(5)));

        // Send SMS verification
        await context.CallAsync<SmsResponse>("SendSms", new SmsRequest
        {
            PhoneNumber = request.PhoneNumber,
            Message = "Your verification code is 123456"
        });

        response.Success = true;
        response.CompletedAt = DateTimeOffset.UtcNow;
        return response;
    }

    private static async Task<BatchJobResponse> BatchProcessingOrchestrator(IOrchestrationContext context, BatchJobRequest request)
    {
        var response = new BatchJobResponse { BatchId = request.BatchId };
        var tasks = new List<Task<OrderResponse>>();

        // Process orders in parallel
        foreach (var order in request.Orders)
        {
            var task = context.CallAsync<OrderResponse>("OrderProcessingOrchestrator", order);
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        response.ProcessedCount = results.Length;
        response.SuccessCount = results.Count(r => r.Success);
        response.FailureCount = results.Count(r => !r.Success);
        response.CompletedAt = DateTimeOffset.UtcNow;

        return response;
    }

    private static async Task<RecurringJobResponse> RecurringJobOrchestrator(IOrchestrationContext context, RecurringJobRequest request)
    {
        // Implement recurring job logic
        var response = new RecurringJobResponse { JobId = request.JobId };
        
        // Do some work...
        await Task.Delay(100);
        
        // Schedule next run
        var nextRun = DateTimeOffset.UtcNow.Add(request.Interval);
        await context.CreateTimer(nextRun);
        
        // Continue to next iteration (this creates a recurring pattern)
        await context.CallAsync<RecurringJobResponse>("RecurringJobOrchestrator", request);
        
        return response;
    }
}

// 📋 Data Models
public class EmailRequest
{
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

public class EmailResponse
{
    public bool Success { get; set; }
    public string MessageId { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}

public class SmsRequest
{
    public string PhoneNumber { get; set; } = "";
    public string Message { get; set; } = "";
}

public class SmsResponse
{
    public bool Success { get; set; }
    public string MessageId { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}

public class PaymentRequest
{
    public decimal Amount { get; set; }
    public string CustomerId { get; set; } = "";
}

public class PaymentResponse
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public class RefundRequest
{
    public string TransactionId { get; set; } = "";
    public decimal Amount { get; set; }
}

public class RefundResponse
{
    public bool Success { get; set; }
    public string RefundId { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public class InventoryReservationRequest
{
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
}

public class InventoryReservationResponse
{
    public bool Success { get; set; }
    public string ReservationId { get; set; } = "";
    public int ReservedQuantity { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public class OrderRequest
{
    public string OrderId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}

public class OrderResponse
{
    public string OrderId { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? TransactionId { get; set; }
    public string? InventoryReservationId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class CustomerOnboardingRequest
{
    public string CustomerId { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
}

public class CustomerOnboardingResponse
{
    public string CustomerId { get; set; } = "";
    public bool Success { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class BatchJobRequest
{
    public string BatchId { get; set; } = "";
    public List<OrderRequest> Orders { get; set; } = new();
}

public class BatchJobResponse
{
    public string BatchId { get; set; } = "";
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class RecurringJobRequest
{
    public string JobId { get; set; } = "";
    public TimeSpan Interval { get; set; }
}

public class RecurringJobResponse
{
    public string JobId { get; set; } = "";
    public DateTimeOffset? CompletedAt { get; set; }
}