using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Models;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

namespace Asynkron.DurableFunctions.Examples;

/// <summary>
/// Example demonstrating the new disposable lease functionality.
/// Shows how to use the 'await using' pattern for automatic lease cleanup.
/// </summary>
public class DisposableLeaseExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Disposable Lease Example ===");

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        loggerFactory.CreateLogger<DurableFunctionRuntime>();
        var stateStoreLogger = loggerFactory.CreateLogger<SqliteStateStore>();

        // Create SQLite StateStore (in-memory for demo)
        var connectionString = "Data Source=:memory:";
        using var stateStore = new SqliteStateStore(connectionString, stateStoreLogger);

        // Create a sample orchestrator state
        var state = new DurableStateDto("parent", DateTimeOffset.UtcNow, "TestOrchestrator", "{}");
        await stateStore.SaveStateAsync(state);

        var hostId = "example-host";
        var leaseDuration = TimeSpan.FromMinutes(5);

        Console.WriteLine($"Created orchestrator state with ID: {state.InstanceId}");
        Console.WriteLine();

        // Example 1: Basic disposable lease usage
        Console.WriteLine("Example 1: Basic disposable lease usage");
        await ExampleBasicDisposableLease(stateStore, state.InstanceId, hostId, leaseDuration);
        Console.WriteLine();

        // Example 2: Error handling with disposable leases
        Console.WriteLine("Example 2: Error handling with disposable leases");
        await ExampleErrorHandlingWithDisposableLease(stateStore, state.InstanceId, hostId, leaseDuration);
        Console.WriteLine();

        // Example 3: Comparison with manual lease management
        Console.WriteLine("Example 3: Comparison with manual lease management");
        await ExampleManualVsDisposableLeaseComparison(stateStore, state.InstanceId, hostId, leaseDuration);
        Console.WriteLine();

        Console.WriteLine("=== All examples completed ===");
    }

    private static async Task ExampleBasicDisposableLease(IStateStore stateStore, string instanceId, string hostId,
        TimeSpan leaseDuration)
    {
        Console.WriteLine("Using disposable lease with 'await using' pattern:");

        // The new disposable pattern - lease is automatically released
        var claimResult = await stateStore.TryClaimDisposableLeaseAsync(instanceId, hostId, leaseDuration);

        if (claimResult.Success && claimResult.DisposableLease != null)
        {
            await using var lease = claimResult.DisposableLease;

            Console.WriteLine($"✅ Lease claimed successfully by {lease.LeaseOwner}");
            Console.WriteLine($"   Lease expires at: {lease.LeaseExpiresAt}");
            Console.WriteLine($"   Lease version: {lease.Version}");

            // Simulate some work
            Console.WriteLine("   Performing orchestrator work...");
            await Task.Delay(1000);

            Console.WriteLine("   Work completed!");
        } // 🎉 Lease is automatically released here when the using block exits!

        Console.WriteLine("✅ Lease has been automatically released");

        // Verify another host can claim the lease
        var verifyResult = await stateStore.TryClaimLeaseAsync(instanceId, "verification-host", leaseDuration);
        Console.WriteLine($"✅ Verification: Another host can claim lease = {verifyResult.Success}");

        if (verifyResult.Success)
        {
            await stateStore.ReleaseLeaseAsync(instanceId, "verification-host", verifyResult.Lease!.Version);
        }
    }

    private static async Task ExampleErrorHandlingWithDisposableLease(IStateStore stateStore, string instanceId,
        string hostId, TimeSpan leaseDuration)
    {
        Console.WriteLine("Demonstrating error handling with disposable leases:");

        var claimResult = await stateStore.TryClaimDisposableLeaseAsync(instanceId, hostId, leaseDuration);

        if (claimResult.Success && claimResult.DisposableLease != null)
        {
            try
            {
                await using var lease = claimResult.DisposableLease;

                Console.WriteLine($"✅ Lease claimed by {lease.LeaseOwner}");
                Console.WriteLine("   Simulating an error during work...");

                // Simulate work that throws an exception
                await Task.Delay(500);
                throw new InvalidOperationException("Simulated orchestrator error!");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"❌ Error occurred: {ex.Message}");
                Console.WriteLine("   But lease will still be automatically released!");
            } // 🎉 Lease is automatically released even when exceptions occur!
        }

        Console.WriteLine("✅ Lease was automatically released despite the error");

        // Verify another host can claim the lease
        var verifyResult = await stateStore.TryClaimLeaseAsync(instanceId, "verification-host", leaseDuration);
        Console.WriteLine($"✅ Verification: Another host can claim lease = {verifyResult.Success}");

        if (verifyResult.Success)
        {
            await stateStore.ReleaseLeaseAsync(instanceId, "verification-host", verifyResult.Lease!.Version);
        }
    }

    private static async Task ExampleManualVsDisposableLeaseComparison(IStateStore stateStore, string instanceId,
        string hostId, TimeSpan leaseDuration)
    {
        Console.WriteLine("Comparison: Manual lease management vs Disposable leases");
        Console.WriteLine();

        // Old way - Manual lease management (still supported)
        Console.WriteLine("❌ Old way - Manual lease management:");
        var manualClaimResult = await stateStore.TryClaimLeaseAsync(instanceId, hostId, leaseDuration);

        if (manualClaimResult.Success && manualClaimResult.Lease != null)
        {
            try
            {
                Console.WriteLine($"   Lease claimed manually by {manualClaimResult.Lease.LeaseOwner}");
                Console.WriteLine("   Performing work...");
                await Task.Delay(500);
                Console.WriteLine("   Work completed");
            }
            finally
            {
                // Must remember to manually release the lease
                var released = await stateStore.ReleaseLeaseAsync(instanceId, hostId, manualClaimResult.Lease.Version);
                Console.WriteLine($"   Manual lease release: {(released ? "✅ Success" : "❌ Failed")}");
            }
        }

        Console.WriteLine();

        // New way - Disposable lease management
        Console.WriteLine("✅ New way - Disposable lease management:");
        var disposableClaimResult = await stateStore.TryClaimDisposableLeaseAsync(instanceId, hostId, leaseDuration);

        if (disposableClaimResult.Success && disposableClaimResult.DisposableLease != null)
        {
            await using var lease = disposableClaimResult.DisposableLease;

            Console.WriteLine($"   Lease claimed with disposable by {lease.LeaseOwner}");
            Console.WriteLine("   Performing work...");
            await Task.Delay(500);
            Console.WriteLine("   Work completed");
        } // 🎉 Automatic release - no need to remember!

        Console.WriteLine("   Automatic lease release: ✅ Success (guaranteed!)");

        Console.WriteLine();
        Console.WriteLine("Benefits of disposable leases:");
        Console.WriteLine("• 🔒 Guaranteed cleanup - no forgotten releases");
        Console.WriteLine("• 🛡️  Exception safety - releases even on errors");
        Console.WriteLine("• 📖 Cleaner code - no try/finally blocks needed");
        Console.WriteLine("• 🔧 'await using' syntax - familiar C# pattern");
    }
}