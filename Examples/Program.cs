using System.Linq;

namespace Asynkron.DurableFunctions.Examples;

internal class Program
{
    private static async Task Main(string[] args)
    {
        if (await TryHandleCommandLineAsync(args))
        {
            return;
        }

        Console.WriteLine("Asynkron.DurableFunctions - Examples");
        Console.WriteLine("====================================\n");

        try
        {
            // Run the Core vs Azure Adapter comparison example FIRST
            await CoreVsAzureAdapterExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run the original function call state example
            await FunctionCallStateExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run the new auto-registration example
            await AutoRegistrationExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run the new typed orchestrator example
            await TypedOrchestratorExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run the SQLite StateStore example
            await SqliteExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run the new Disposable Lease example
            await DisposableLeaseExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run the Azure compatibility example
            await AzureCompatibilityExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Run the new orchestration client example
            await OrchestrationClientExample.RunExample();

            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // Note: Some examples are excluded when building with NuGet references
            // as they use features not available in the public NuGet package
            // await ChaosEngineeringExample.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running example: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        WaitForExitIfInteractive();
    }

    private static async Task<bool> TryHandleCommandLineAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        var mode = args[0];
        switch (mode.ToLowerInvariant())
        {
            case "postgresql":
            case "postgresql-api":
                Console.WriteLine("🐘 PostgreSQL Web API Example not available with NuGet references");
                Console.WriteLine("💡 This example uses features not available in the public NuGet package");
                return true;
                // await WebApiPostgreSqlExample.RunAsync(args.Skip(1).ToArray());
                return true;
            case "postgresql-cli":
                Console.WriteLine("🐘 PostgreSQL Console Example not available with NuGet references");
                Console.WriteLine("💡 This example uses features not available in the public NuGet package");
                return true;
                // await PostgreSqlExample.RunAsync();
                return true;
            case "otel":
            case "otel-collector":
                Console.WriteLine("🎯 Running OpenTelemetry collector sample");
                await OpenTelemetryCollectorExample.RunAsync(args.Skip(1).ToArray());
                return true;
            default:
                return false;
        }
    }

    private static void WaitForExitIfInteractive()
    {
        if (!Console.IsInputRedirected)
        {
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
