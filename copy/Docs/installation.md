# Installation Guide

Get Asynkron.DurableFunctions up and running in your .NET project.

## 📋 Prerequisites

- **.NET 6.0 or later** (recommended: .NET 8.0)
- **C# 10 or later** for best experience
- **Visual Studio 2022**, **Visual Studio Code**, or **Rider**

## 📦 Package Installation

### Core Package
Install the main package via NuGet:

```bash
dotnet add package Asynkron.DurableFunctions
```

### Optional Packages

#### SQLite Storage (Recommended for Production)
```bash
dotnet add package Asynkron.DurableFunctions.SQLite
```

#### Azure Functions Compatibility
```bash
dotnet add package Asynkron.DurableFunctions.AzureAdapter
```

#### ASP.NET Core Integration
```bash
dotnet add package Asynkron.DurableFunctions.AspNetCore
```

## 🏗️ Project Setup

### Console Application
Create a new console application:

```bash
dotnet new console -n MyDurableFunctions
cd MyDurableFunctions
dotnet add package Asynkron.DurableFunctions
```

**Program.cs:**
```csharp
using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

var stateStore = new InMemoryStateStore();
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var runtime = new DurableFunctionRuntime(
    stateStore,
    loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory);

// Your orchestrations and activities here...

Console.WriteLine("Starting Durable Functions runtime...");
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await runtime.RunAndPollAsync(cts.Token);
```

### ASP.NET Core Web Application
Create a new web application:

```bash
dotnet new web -n MyDurableFunctionsWeb
cd MyDurableFunctionsWeb
dotnet add package Asynkron.DurableFunctions
```

**Program.cs:**
```csharp
using Asynkron.DurableFunctions;

var builder = WebApplication.CreateBuilder(args);

// Add Durable Functions services
builder.Services.AddSingleton<IStateStore>(sp => 
    new InMemoryStateStore()); // Use SQLite for production
builder.Services.AddSingleton<DurableFunctionRuntime>();

var app = builder.Build();

// Configure API endpoints
app.MapPost("/orchestrations/{name}", async (string name, object input, DurableFunctionRuntime runtime) =>
{
    var instanceId = Guid.NewGuid().ToString();
    await runtime.TriggerAsync(instanceId, name, input);
    return Results.Ok(new { InstanceId = instanceId });
});

// Start the runtime in the background
var runtime = app.Services.GetRequiredService<DurableFunctionRuntime>();
_ = Task.Run(() => runtime.RunAndPollAsync(CancellationToken.None));

app.Run();
```

### Worker Service
Create a background worker service:

```bash
dotnet new worker -n MyDurableFunctionsWorker
cd MyDurableFunctionsWorker
dotnet add package Asynkron.DurableFunctions
```

**Program.cs:**
```csharp
using Asynkron.DurableFunctions;

var builder = Host.CreateApplicationBuilder(args);

// Add Durable Functions services
builder.Services.AddSingleton<IStateStore>(sp => 
    new SqliteStateStore("Data Source=durable_functions.db"));
builder.Services.AddSingleton<DurableFunctionRuntime>();
builder.Services.AddHostedService<DurableFunctionWorker>();

var host = builder.Build();
host.Run();
```

**Worker.cs:**
```csharp
using Asynkron.DurableFunctions;

public class DurableFunctionWorker : BackgroundService
{
    private readonly DurableFunctionRuntime _runtime;
    private readonly ILogger<DurableFunctionWorker> _logger;

    public DurableFunctionWorker(DurableFunctionRuntime runtime, ILogger<DurableFunctionWorker> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Durable Functions Worker starting");
        
        // Register your orchestrations and activities here
        RegisterFunctions();
        
        await _runtime.RunAndPollAsync(stoppingToken);
    }

    private void RegisterFunctions()
    {
        // Register your functions here
        _runtime.RegisterFunction<string, string>("SampleFunction", async input =>
        {
            return $"Processed: {input}";
        });
    }
}
```

## 🗄️ Storage Configuration

### In-Memory Storage (Development)
Perfect for development and testing:

```csharp
var stateStore = new InMemoryStateStore();
```

**Pros:**
- Fast setup
- No external dependencies
- Great for testing

**Cons:**
- Data lost on restart
- Not suitable for production

### SQLite Storage (Production)
Recommended for most production scenarios:

```csharp
var stateStore = new SqliteStateStore("Data Source=durable_functions.db");
```

**Pros:**
- Persistent storage
- No external database required
- Easy deployment
- Good performance

**Cons:**
- Single-file database
- Not suitable for high-scale distributed scenarios

### Custom Storage
Implement your own storage backend:

```csharp
public class CosmosDbStateStore : IStateStore
{
    // Implement your storage logic
    public Task SaveStateAsync(string instanceId, object state) { /* ... */ }
    public Task<T> LoadStateAsync<T>(string instanceId) { /* ... */ }
    // ... other methods
}

var stateStore = new CosmosDbStateStore();
```

## 🔧 Configuration Options

### Basic Configuration
```csharp
var runtime = new DurableFunctionRuntime(
    stateStore: new SqliteStateStore("Data Source=app.db"),
    logger: loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory
);
```

### Advanced Configuration
```csharp
var options = new DurableFunctionRuntimeOptions
{
    MaxConcurrentOrchestrations = 100,
    OrchestrationPollingInterval = TimeSpan.FromSeconds(1),
    ActivityTimeout = TimeSpan.FromMinutes(5),
    RetryOptions = new RetryOptions
    {
        MaxRetryCount = 3,
        RetryDelay = TimeSpan.FromSeconds(1)
    }
};

var runtime = new DurableFunctionRuntime(stateStore, logger, options, loggerFactory);
```

## 🔍 Logging Configuration

### Console Logging
```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information)
);
```

### Serilog Integration
```bash
dotnet add package Serilog.Extensions.Logging
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/durable-functions.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSerilog()
);
```

### ASP.NET Core Logging
```csharp
var builder = WebApplication.CreateBuilder(args);

// Built-in logging configuration
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);
```

## 📁 Project Structure

Recommended project structure:

```
MyDurableFunctions/
├── Program.cs                 # Application entry point
├── Orchestrators/
│   ├── OrderProcessingOrchestrator.cs
│   └── PaymentOrchestrator.cs
├── Activities/
│   ├── OrderActivities.cs
│   └── PaymentActivities.cs
├── Models/
│   ├── OrderRequest.cs
│   └── PaymentRequest.cs
├── Configuration/
│   └── DurableFunctionConfiguration.cs
└── appsettings.json          # Configuration file
```

## 🐳 Docker Support

### Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyDurableFunctions.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyDurableFunctions.dll"]
```

### Docker Compose
```yaml
version: '3.8'
services:
  durable-functions:
    build: .
    ports:
      - "8080:80"
    volumes:
      - ./data:/app/data
    environment:
      - ConnectionStrings__StateStore=Data Source=/app/data/durable_functions.db
```

## ✅ Verification

Test your installation with this simple verification:

```csharp
using Asynkron.DurableFunctions;
using Microsoft.Extensions.Logging;

// Verify installation
var stateStore = new InMemoryStateStore();
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var runtime = new DurableFunctionRuntime(
    stateStore,
    loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory);

// Register a simple test function
runtime.RegisterFunction<string, string>("TestFunction", async input =>
{
    return $"Hello from Asynkron.DurableFunctions! Input: {input}";
});

runtime.RegisterOrchestratorFunction<string, string>("TestOrchestrator", async context =>
{
    var input = context.GetInput<string>();
    return await context.CallFunction<string>("TestFunction", input);
});

// Test it
await runtime.TriggerAsync("test-instance", "TestOrchestrator", "World");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await runtime.RunAndPollAsync(cts.Token);

Console.WriteLine("✅ Installation verified successfully!");
```

## 🆘 Troubleshooting

### Common Issues

#### Package Not Found
**Error:** `Package 'Asynkron.DurableFunctions' could not be found`
**Solution:** Ensure you're using the correct package name and have internet connectivity.

#### Missing Dependencies
**Error:** `The type or namespace 'DurableFunctionRuntime' could not be found`
**Solution:** Add the required using statement: `using Asynkron.DurableFunctions;`

#### SQLite Issues on Linux
**Error:** `SQLite interop library not found`
**Solution:** Install the native SQLite library:
```bash
# Ubuntu/Debian
sudo apt-get install libsqlite3-dev

# CentOS/RHEL
sudo yum install sqlite-devel
```

#### Runtime Not Starting
**Error:** Runtime appears to hang or not process orchestrations
**Solution:** Ensure you're calling `RunAndPollAsync()` and passing a valid cancellation token.

## 🎯 Next Steps

1. **[Quick Start](getting-started.md)** - Build your first durable function
2. **[Examples](../Examples/README.md)** - Explore sample implementations
3. **[Core Concepts](concepts.md)** - Understand the framework
4. **[Deployment](deployment.md)** - Deploy to production

Your installation is complete! Start building resilient, long-running workflows with Asynkron.DurableFunctions.