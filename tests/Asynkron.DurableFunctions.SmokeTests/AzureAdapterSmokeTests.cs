using Asynkron.DurableFunctions.AzureAdapter.Attributes;
using Asynkron.DurableFunctions.AzureAdapter.Core;
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Asynkron.DurableFunctions.SmokeTests;

/// <summary>
/// Basic smoke tests to ensure the Azure Adapter package builds and loads correctly.
/// These tests verify that the essential components can be instantiated and used.
/// </summary>
public class AzureAdapterSmokeTests
{
    [Fact]
    public void CanCreateDurableFunctionRuntime()
    {
        // Arrange & Act
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);
        
        // Assert
        Assert.NotNull(runtime);
    }
    
    [Fact]
    public void CanCreateAzureCompatibleOrchestrator()
    {
        // Arrange
        var stateStore = new InMemoryStateStore();
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
        
        var runtime = new DurableFunctionRuntime(stateStore, logger, loggerFactory: loggerFactory);
        var testFunctions = new TestAzureFunctions();
        
        // Act & Assert - Should not throw
        var orchestratorMethod = typeof(TestAzureFunctions).GetMethod(nameof(TestAzureFunctions.TestOrchestrator))!;
        var activityMethod = typeof(TestAzureFunctions).GetMethod(nameof(TestAzureFunctions.TestActivity))!;
        
        runtime.RegisterAzureOrchestrator(orchestratorMethod, testFunctions);
        runtime.RegisterAzureActivity(activityMethod, testFunctions);
    }
    
    [Fact]
    public void AzureAdapterAttributesCanBeApplied()
    {
        // Act & Assert - Should not throw during compilation/reflection
        var functions = new TestAzureFunctions();
        var type = functions.GetType();
        var orchestratorMethod = type.GetMethod(nameof(TestAzureFunctions.TestOrchestrator));
        var activityMethod = type.GetMethod(nameof(TestAzureFunctions.TestActivity));
        
        Assert.NotNull(orchestratorMethod);
        Assert.NotNull(activityMethod);
        
        // Verify attributes are present
        var orchestratorAttr = orchestratorMethod!.GetCustomAttributes(typeof(FunctionNameAttribute), false);
        var activityAttr = activityMethod!.GetCustomAttributes(typeof(FunctionNameAttribute), false);
        
        Assert.Single(orchestratorAttr);
        Assert.Single(activityAttr);
    }
}

/// <summary>
/// Test functions using Azure-compatible attributes for smoke testing
/// </summary>
public class TestAzureFunctions
{
    [FunctionName("TestOrchestrator")]
    public async Task<string> TestOrchestrator([DurableOrchestrationTrigger] IDurableOrchestrationContext context)
    {
        // Simple test orchestrator that doesn't call other functions to avoid runtime complexity
        var input = context.GetInput<string>() ?? "test";
        await Task.Delay(1); // Minimal async work
        return $"Orchestrator processed: {input}";
    }
    
    [FunctionName("TestActivity")]
    public async Task<string> TestActivity([DurableActivityTrigger] IDurableActivityContext context)
    {
        var input = context.GetInput<string>() ?? "test";
        await Task.Delay(1); // Minimal async work
        return $"Activity processed: {input}";
    }
}