# Asynkron.DurableFunctions Examples

This directory contains comprehensive examples demonstrating the capabilities of Asynkron.DurableFunctions.

## 🚀 Quick Start

Each example is self-contained and demonstrates a specific pattern or feature.

## 📝 Available Examples

### Basic Examples

1. **[HelloWorld.cs](HelloWorld.cs)** - The simplest possible durable function
   - Basic orchestrator and activity function
   - In-memory state store
   - Simple input/output patterns

2. **[DataPipeline.cs](DataPipeline.cs)** - Multi-step data processing
   - Sequential function calls
   - Data transformation workflow
   - Clean error propagation

### Workflow Patterns

3. **[SequentialWorkflow.cs](SequentialWorkflow.cs)** - Order processing workflow
   - Step-by-step processing
   - Strongly typed inputs/outputs
   - Real-world business scenario

4. **[ParallelProcessing.cs](ParallelProcessing.cs)** - Fan-out/Fan-in pattern
   - Concurrent function execution
   - Result aggregation
   - Performance optimization

### Advanced Patterns

5. **[DurableTimers.cs](DurableTimers.cs)** - Time-based workflows
   - Long-running processes with delays
   - Email sequence automation
   - Timer creation and management

6. **[HumanApproval.cs](HumanApproval.cs)** - External event handling
   - Human-in-the-loop workflows
   - External event waiting
   - Timeout handling

7. **[ErrorHandling.cs](ErrorHandling.cs)** - Resilience patterns
   - Retry mechanisms
   - Graceful degradation
   - Fallback strategies

### Composition Patterns

8. **[SubOrchestrations.cs](SubOrchestrations.cs)** - Orchestrator composition
   - Parent-child orchestrator relationships
   - Complex workflow breakdown
   - Nested orchestration patterns

9. **[EternalOrchestrations.cs](EternalOrchestrations.cs)** - Long-running monitors
   - Infinite loop patterns
   - System monitoring
   - Periodic maintenance tasks

### Migration Support

10. **[AzureCompatibility.cs](AzureCompatibility.cs)** - Azure Functions compatibility
    - Drop-in replacement patterns
    - Familiar Azure APIs
    - Migration examples

## 🏃‍♂️ Running Examples

### Run Individual Examples

```bash
# Navigate to Examples directory
cd Examples

# Run specific example
dotnet run hello          # Hello World
dotnet run sequential     # Sequential Workflow
dotnet run parallel       # Parallel Processing
dotnet run timers         # Durable Timers
dotnet run approval       # Human Approval
dotnet run error          # Error Handling
dotnet run sub           # Sub-Orchestrations
dotnet run pipeline      # Data Pipeline
dotnet run eternal       # Eternal Orchestrations
dotnet run azure         # Azure Compatibility
```

### Run All Examples

```bash
dotnet run all
```

## 🎯 Example Categories

### **Beginner Examples**
- HelloWorld
- DataPipeline
- SequentialWorkflow

### **Intermediate Examples**
- ParallelProcessing
- DurableTimers
- HumanApproval

### **Advanced Examples**
- ErrorHandling
- SubOrchestrations
- EternalOrchestrations
- AzureCompatibility

## 🛠️ Key Concepts Demonstrated

### Core Patterns
- **CallFunction** - Primary activity invocation
- **Orchestrator registration** - Workflow definition
- **State management** - Persistence and recovery
- **Input/Output handling** - Type-safe data flow

### Advanced Features
- **External events** - Human interaction patterns
- **Durable timers** - Long-running delays
- **Sub-orchestrations** - Workflow composition
- **Error handling** - Resilience patterns
- **Parallel execution** - Performance optimization

### Integration Patterns
- **In-memory storage** - Development scenarios
- **SQLite storage** - Production persistence
- **Azure compatibility** - Migration support
- **Logging integration** - Observability

## 🏗️ Project Structure

```
Examples/
├── README.md                    # This file
├── Program.cs                   # Main runner program
├── HelloWorld.cs               # Basic example
├── SequentialWorkflow.cs       # Order processing
├── ParallelProcessing.cs       # Fan-out/fan-in
├── DurableTimers.cs           # Time-based workflows
├── HumanApproval.cs           # External events
├── ErrorHandling.cs           # Resilience patterns
├── SubOrchestrations.cs       # Composition
├── DataPipeline.cs            # Data processing
├── EternalOrchestrations.cs   # Monitoring patterns
└── AzureCompatibility.cs      # Azure migration
```

## 🎨 Customization

Each example is designed to be:
- **Self-contained** - No external dependencies
- **Well-documented** - Clear comments and explanations
- **Easily modifiable** - Simple to adapt for your needs
- **Production-ready** - Follows best practices

## 🔗 Next Steps

1. **Start with HelloWorld** - Understand the basics
2. **Try SequentialWorkflow** - See real-world patterns
3. **Explore ParallelProcessing** - Learn performance optimization
4. **Study ErrorHandling** - Understand resilience
5. **Experiment with timers** - Master time-based workflows

## 💡 Tips

- Each example uses `Console.WriteLine` for visibility
- Examples are designed to complete quickly for demonstration
- Production timeouts would be much longer (hours/days instead of seconds)
- State stores are configurable (in-memory for demos, SQLite for production)

## 🆘 Need Help?

- Check the main [README.md](../README.md) for complete documentation
- Review the source code comments for detailed explanations
- Each example demonstrates a specific pattern - mix and match as needed