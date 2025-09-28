# Asynkron.DurableFunctions Documentation

Welcome to the Asynkron.DurableFunctions documentation. This library provides a clone of Azure Durable Functions hosted in vanilla ASP.NET with full compatibility for Azure Durable Functions patterns.

## Features

- **Durable Orchestrations**: Build complex workflows that can survive process restarts
- **Activity Functions**: Define individual units of work that orchestrations can call
- **External Events**: Send events to running orchestrations from external systems  
- **Timers**: Schedule orchestrations to resume at specific times
- **Sub-orchestrations**: Compose complex workflows from smaller orchestrations
- **Multiple Storage Backends**: Choose from in-memory, SQLite, or PostgreSQL storage
- **Azure Compatibility**: Drop-in replacement for Azure Durable Functions APIs
- **Concurrent Execution**: Safe multi-host execution with lease-based coordination

## Quick Start

### Installation

```bash
dotnet add package Asynkron.DurableFunctions
```

### Basic Usage

```csharp
// Setup the runtime
var stateStore = new InMemoryStateStore();
var runtime = new DurableFunctionRuntime(stateStore, logger);

// Register functions
runtime.RegisterJsonFunction("SayHello", async (context, input) =>
{
    return $"Hello, {input}!";
});

runtime.RegisterJsonOrchestrator("HelloWorkflow", async (context, input) =>
{
    var result = await context.CallAsync<string>("SayHello", input);
    return result;
});

// Start an orchestration
await runtime.TriggerAsync("workflow-1", "HelloWorkflow", "World");

// Run the runtime
using var cts = new CancellationTokenSource();
await runtime.RunAndPollAsync(cts.Token);
```

## Storage Options

### In-Memory (Development)
```csharp
services.AddDurableFunctionsWithInMemory();
```

### SQLite (Single Node)
```csharp
services.AddDurableFunctionsWithSQLite("Data Source=workflows.db");
```

### PostgreSQL (Production)
```csharp
services.AddDurableFunctionsWithPostgreSQL("Host=localhost;Database=workflows");
```

## API Documentation

Explore the [API Documentation](api/) for detailed information about all classes, methods, and interfaces.

## Examples

Check out the [examples directory](../examples/) for complete working examples demonstrating various features and patterns.

## Azure Compatibility

Use the Azure adapter for drop-in compatibility with Azure Durable Functions:

```bash
dotnet add package Asynkron.DurableFunctions.AzureAdapter
```

This provides familiar Azure attributes and interfaces while running on your own infrastructure.