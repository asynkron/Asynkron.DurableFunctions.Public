# Activity Functions

Activity functions are the workhorses of durable workflows. They perform the actual work that orchestrators coordinate - calling external services, processing data, sending notifications, and more.

## 🔧 What is an Activity?

An activity function is a function that:
- **Performs actual work** - Makes API calls, processes data, accesses databases
- **Is called by orchestrators** - Invoked via `context.CallFunction()` or `context.CallAsync()`
- **Can be retried** - Automatically retried on transient failures
- **Runs independently** - Each invocation is isolated and stateless
- **Returns results** - Provides data back to the orchestrator

## 📝 Registering Activities

### Simple Registration with Context

Activities can receive an `IFunctionContext` parameter to access logging and instance information:

```csharp
runtime.RegisterJsonFunction("SendEmail", async (context, input) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Sending email for instance {InstanceId}", context.InstanceId);
    
    // Parse input
    var emailRequest = JsonSerializer.Deserialize<EmailRequest>(input);
    
    // Perform the actual work
    await emailService.SendAsync(emailRequest.To, emailRequest.Subject, emailRequest.Body);
    
    logger.LogInformation("Email sent successfully");
    return "Email sent";
});
```

### Simple Registration without Context

For simple activities that don't need logging or context information:

```csharp
runtime.RegisterFunction<string, string>("ProcessData", async (input) =>
{
    // Process the input
    var result = input.ToUpperInvariant();
    return result;
});
```

### Strongly Typed Registration with Context

Using strongly typed inputs and outputs with context access:

```csharp
runtime.RegisterFunction<EmailRequest, EmailResult>("SendTypedEmail", async (context, request) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Sending email to {Recipient} for instance {InstanceId}", 
        request.To, context.InstanceId);
    
    await emailService.SendAsync(request.To, request.Subject, request.Body);
    
    return new EmailResult
    {
        Success = true,
        MessageId = Guid.NewGuid().ToString(),
        SentAt = DateTime.UtcNow
    };
});
```

### Attribute-Based Registration

Using attributes for auto-discovery and registration:

```csharp
public class EmailActivities
{
    [Function("SendEmail")]
    public async Task<EmailResult> SendEmail(
        [FunctionTrigger] EmailRequest request,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger();
        logger.LogInformation("Sending email to {Recipient} for instance {InstanceId}", 
            request.To, executionContext.InstanceId);
        
        await emailService.SendAsync(request.To, request.Subject, request.Body);
        
        return new EmailResult
        {
            Success = true,
            MessageId = Guid.NewGuid().ToString()
        };
    }
}

// Register all activities from the class
runtime.ScanAndRegister(typeof(EmailActivities).Assembly);
```

## 🧩 Function Context

The `IFunctionContext` parameter provides access to:

### Instance ID
Get the unique identifier for the orchestration instance:
```csharp
runtime.RegisterJsonFunction("LogActivity", (context, input) =>
{
    Console.WriteLine($"Processing for instance: {context.InstanceId}");
    return Task.FromResult("Done");
});
```

### Logging
Get a replay-safe logger for structured logging:
```csharp
runtime.RegisterJsonFunction("ComplexActivity", async (context, input) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Activity started for instance {InstanceId}", context.InstanceId);
    
    try
    {
        // Do work
        await DoWorkAsync(input);
        logger.LogInformation("Activity completed successfully");
        return "Success";
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Activity failed for instance {InstanceId}", context.InstanceId);
        throw;
    }
});
```

## 🎯 Activity Patterns

### 1. External API Calls

Always call external services from activities, not from orchestrators:

```csharp
runtime.RegisterFunction<PaymentRequest, PaymentResult>("ProcessPayment", async (context, request) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Processing payment for order {OrderId}, instance {InstanceId}", 
        request.OrderId, context.InstanceId);
    
    try
    {
        var response = await paymentService.ChargeAsync(request);
        logger.LogInformation("Payment successful: {TransactionId}", response.TransactionId);
        return response;
    }
    catch (PaymentException ex)
    {
        logger.LogWarning(ex, "Payment failed for order {OrderId}", request.OrderId);
        throw;
    }
});
```

### 2. Database Operations

Perform database operations in activities:

```csharp
runtime.RegisterFunction<Order, string>("SaveOrder", async (context, order) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Saving order {OrderId} to database, instance {InstanceId}", 
        order.Id, context.InstanceId);
    
    await database.Orders.InsertAsync(order);
    
    logger.LogInformation("Order {OrderId} saved successfully", order.Id);
    return order.Id;
});
```

### 3. Data Transformation

Transform data in activities:

```csharp
runtime.RegisterFunction<CustomerData, EnrichedCustomer>("EnrichCustomerData", async (context, customer) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Enriching customer data for {CustomerId}, instance {InstanceId}", 
        customer.Id, context.InstanceId);
    
    // Call external service
    var creditScore = await creditService.GetScoreAsync(customer.Id);
    var preferences = await preferenceService.GetPreferencesAsync(customer.Id);
    
    return new EnrichedCustomer
    {
        Id = customer.Id,
        Name = customer.Name,
        Email = customer.Email,
        CreditScore = creditScore,
        Preferences = preferences
    };
});
```

### 4. Notifications

Send notifications from activities:

```csharp
runtime.RegisterFunction<NotificationRequest, bool>("SendNotification", async (context, request) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Sending {Type} notification to {Recipient}, instance {InstanceId}", 
        request.Type, request.Recipient, context.InstanceId);
    
    switch (request.Type)
    {
        case "Email":
            await emailService.SendAsync(request);
            break;
        case "SMS":
            await smsService.SendAsync(request);
            break;
        case "Push":
            await pushService.SendAsync(request);
            break;
    }
    
    logger.LogInformation("Notification sent successfully");
    return true;
});
```

### 5. File Operations

Handle file operations in activities:

```csharp
runtime.RegisterFunction<FileRequest, FileResult>("ProcessFile", async (context, request) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Processing file {FileName}, instance {InstanceId}", 
        request.FileName, context.InstanceId);
    
    // Download file
    var content = await storageService.DownloadAsync(request.FileUrl);
    
    // Process file
    var processedContent = await ProcessContentAsync(content);
    
    // Upload result
    var resultUrl = await storageService.UploadAsync(processedContent, $"processed-{request.FileName}");
    
    logger.LogInformation("File processed and uploaded to {Url}", resultUrl);
    
    return new FileResult
    {
        OriginalUrl = request.FileUrl,
        ProcessedUrl = resultUrl,
        ProcessedAt = DateTime.UtcNow
    };
});
```

## 🚨 Best Practices

### ✅ Do's

1. **Use context for logging**:
```csharp
// ✅ Good - structured logging with context
runtime.RegisterJsonFunction("MyActivity", (context, input) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Processing {Input} for instance {InstanceId}", input, context.InstanceId);
    return Task.FromResult("Done");
});
```

2. **Keep activities focused**:
```csharp
// ✅ Good - single responsibility
runtime.RegisterFunction<Order, string>("ValidateOrder", ValidateOrder);
runtime.RegisterFunction<Order, PaymentResult>("ProcessPayment", ProcessPayment);
runtime.RegisterFunction<Order, ShipmentResult>("CreateShipment", CreateShipment);
```

3. **Return meaningful results**:
```csharp
// ✅ Good - detailed result
runtime.RegisterFunction<Order, OrderResult>("ProcessOrder", async (context, order) =>
{
    return new OrderResult
    {
        OrderId = order.Id,
        Status = "Completed",
        ProcessedAt = DateTime.UtcNow,
        ConfirmationNumber = confirmationNumber
    };
});
```

4. **Handle errors appropriately**:
```csharp
// ✅ Good - log and throw
runtime.RegisterFunction<string, string>("RiskyOperation", async (context, input) =>
{
    var logger = context.GetLogger();
    try
    {
        return await PerformOperationAsync(input);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Operation failed for instance {InstanceId}", context.InstanceId);
        throw; // Let orchestrator handle retry
    }
});
```

### ❌ Don'ts

1. **Don't ignore the context parameter**:
```csharp
// ❌ Bad - ignoring context
runtime.RegisterJsonFunction("MyActivity", (_, input) =>
{
    Console.WriteLine($"Processing {input}"); // No structured logging
    return Task.FromResult("Done");
});

// ✅ Good - use context
runtime.RegisterJsonFunction("MyActivity", (context, input) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Processing {Input} for instance {InstanceId}", input, context.InstanceId);
    return Task.FromResult("Done");
});
```

2. **Don't perform orchestration logic in activities**:
```csharp
// ❌ Bad - activity calling other activities
runtime.RegisterFunction<Order, string>("ProcessOrderActivity", async (context, order) =>
{
    var validated = await context.CallAsync<Order>("ValidateOrder", order); // Wrong!
    var charged = await context.CallAsync<Payment>("ChargePayment", validated); // Wrong!
    return "Done";
});
```

3. **Don't maintain state between invocations**:
```csharp
// ❌ Bad - static state
private static int counter = 0;
runtime.RegisterFunction<string, int>("BadActivity", (context, input) =>
{
    counter++; // Don't do this!
    return Task.FromResult(counter);
});
```

4. **Don't use Console.WriteLine instead of logging**:
```csharp
// ❌ Bad - no structured logging
runtime.RegisterJsonFunction("MyActivity", (context, input) =>
{
    Console.WriteLine($"Processing {input}"); // Less useful
    return Task.FromResult("Done");
});

// ✅ Good - structured logging
runtime.RegisterJsonFunction("MyActivity", (context, input) =>
{
    var logger = context.GetLogger();
    logger.LogInformation("Processing {Input} for instance {InstanceId}", input, context.InstanceId);
    return Task.FromResult("Done");
});
```

## 🔄 Error Handling and Retries

Activities automatically support retries through the orchestrator. When an activity throws an exception:

1. The exception is caught by the orchestrator
2. The orchestrator can retry the activity
3. The activity is executed again with the same input

```csharp
// In orchestrator
runtime.RegisterOrchestrator<OrderRequest, string>("OrderWorkflow", async (context, order) =>
{
    try
    {
        // This will be retried automatically if it fails
        var result = await context.CallAsync<string>("ProcessPayment", order);
        return result;
    }
    catch (Exception ex)
    {
        // Handle permanent failure
        await context.CallAsync("NotifyFailure", order.Id);
        return "Failed";
    }
});
```

## 📊 Monitoring Activities

Use structured logging to monitor activity execution:

```csharp
runtime.RegisterFunction<Order, OrderResult>("ProcessOrder", async (context, order) =>
{
    var logger = context.GetLogger();
    var startTime = DateTime.UtcNow;
    
    logger.LogInformation("Processing order {OrderId} started, instance {InstanceId}", 
        order.Id, context.InstanceId);
    
    try
    {
        var result = await ProcessOrderInternalAsync(order);
        
        var duration = DateTime.UtcNow - startTime;
        logger.LogInformation("Processing order {OrderId} completed in {Duration}ms, instance {InstanceId}", 
            order.Id, duration.TotalMilliseconds, context.InstanceId);
        
        return result;
    }
    catch (Exception ex)
    {
        var duration = DateTime.UtcNow - startTime;
        logger.LogError(ex, "Processing order {OrderId} failed after {Duration}ms, instance {InstanceId}", 
            order.Id, duration.TotalMilliseconds, context.InstanceId);
        throw;
    }
});
```

Activities are the building blocks of durable workflows. Keep them focused, use the context for logging and instance tracking, and let orchestrators handle coordination. Next, learn about [Advanced Patterns](patterns.md) to see how activities and orchestrators work together!
