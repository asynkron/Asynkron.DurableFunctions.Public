using Examples;

namespace Examples;

/// <summary>
/// Main program to run all Asynkron.DurableFunctions examples
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 Asynkron.DurableFunctions Examples");
        Console.WriteLine("=====================================");
        
        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var example = args[0].ToLowerInvariant();

        try
        {
            switch (example)
            {
                case "hello":
                case "helloworld":
                    Console.WriteLine("\n📝 Running Hello World Example...\n");
                    await HelloWorldExample.Run();
                    break;

                case "sequential":
                case "workflow":
                    Console.WriteLine("\n🔄 Running Sequential Workflow Example...\n");
                    await SequentialWorkflowExample.Run();
                    break;

                case "parallel":
                case "fanout":
                    Console.WriteLine("\n⚡ Running Parallel Processing Example...\n");
                    await ParallelProcessingExample.Run();
                    break;

                case "timers":
                case "durable":
                    Console.WriteLine("\n⏰ Running Durable Timers Example...\n");
                    await DurableTimersExample.Run();
                    break;

                case "approval":
                case "human":
                    Console.WriteLine("\n👥 Running Human Approval Example...\n");
                    await HumanApprovalExample.Run();
                    break;

                case "error":
                case "resilient":
                    Console.WriteLine("\n🛡️ Running Error Handling Example...\n");
                    await ErrorHandlingExample.Run();
                    break;

                case "sub":
                case "orchestrations":
                    Console.WriteLine("\n🎭 Running Sub-Orchestrations Example...\n");
                    await SubOrchestrationsExample.Run();
                    break;

                case "pipeline":
                case "data":
                    Console.WriteLine("\n📊 Running Data Pipeline Example...\n");
                    await DataPipelineExample.Run();
                    break;

                case "eternal":
                case "monitor":
                    Console.WriteLine("\n♾️ Running Eternal Orchestrations Example...\n");
                    await EternalOrchestrationsExample.Run();
                    break;

                case "azure":
                case "compatibility":
                    Console.WriteLine("\n☁️ Running Azure Compatibility Example...\n");
                    await AzureCompatibilityExample.Run();
                    break;

                case "all":
                    await RunAllExamples();
                    break;

                default:
                    Console.WriteLine($"❌ Unknown example: {example}");
                    ShowHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running example: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine("\n✅ Example completed!");
    }

    static void ShowHelp()
    {
        Console.WriteLine("\nAvailable examples:");
        Console.WriteLine("  hello          - Hello World (simplest example)");
        Console.WriteLine("  sequential     - Sequential workflow processing");
        Console.WriteLine("  parallel       - Parallel processing (fan-out/fan-in)");
        Console.WriteLine("  timers         - Durable timers and delays");
        Console.WriteLine("  approval       - Human approval workflows");
        Console.WriteLine("  error          - Error handling and retry patterns");
        Console.WriteLine("  sub            - Sub-orchestrations");
        Console.WriteLine("  pipeline       - Data pipeline processing");
        Console.WriteLine("  eternal        - Eternal orchestrations (monitoring)");
        Console.WriteLine("  azure          - Azure compatibility example");
        Console.WriteLine("  all            - Run all examples");
        Console.WriteLine("\nUsage:");
        Console.WriteLine("  dotnet run hello");
        Console.WriteLine("  dotnet run sequential");
        Console.WriteLine("  dotnet run all");
    }

    static async Task RunAllExamples()
    {
        var examples = new[]
        {
            ("Hello World", (Func<Task>)HelloWorldExample.Run),
            ("Sequential Workflow", SequentialWorkflowExample.Run),
            ("Parallel Processing", ParallelProcessingExample.Run),
            ("Data Pipeline", DataPipelineExample.Run),
            ("Durable Timers", DurableTimersExample.Run),
            ("Human Approval", HumanApprovalExample.Run),
            ("Error Handling", ErrorHandlingExample.Run),
            ("Sub-Orchestrations", SubOrchestrationsExample.Run),
            ("Eternal Orchestrations", EternalOrchestrationsExample.Run),
            ("Azure Compatibility", AzureCompatibilityExample.Run)
        };

        Console.WriteLine($"🎯 Running all {examples.Length} examples in sequence...\n");

        for (int i = 0; i < examples.Length; i++)
        {
            var (name, runner) = examples[i];
            Console.WriteLine($"[{i + 1}/{examples.Length}] 🚀 Running {name} Example...");
            
            try
            {
                await runner();
                Console.WriteLine($"✅ {name} completed successfully!\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {name} failed: {ex.Message}\n");
            }

            // Small delay between examples
            if (i < examples.Length - 1)
            {
                await Task.Delay(2000);
            }
        }
    }
}