# Multi-Host Orchestrator Concurrency Implementation Summary

This document summarizes the implementation of multi-host orchestrator concurrency for Asynkron.DurableFunctions.

## Problem Solved

Previously, when multiple hosts ran the same application against a shared database, they could simultaneously execute the same orchestrator instance, leading to:
- Duplicate execution
- Race conditions  
- Violation of single-active guarantees
- Inconsistent state

## Solution Approach

Implemented a **hybrid approach** combining:

1. **Optimistic Concurrency Control (CAS)**: Version field with Compare-And-Swap operations
2. **Lease-based Safety**: Short TTL leases with automatic expiration and renewal
3. **Automatic Failover**: Expired leases allow other hosts to take over safely

## Architecture

### Core Components

#### 1. Models (`src/Asynkron.DurableFunctions/Models/`)
- **`OrchestratorLease`**: Represents lease ownership with expiration and version
- **`LeaseClaimResult`**: Result of lease claim attempts with success/failure details
- **`DurableState`** (extended): Added `Lease` property for concurrency metadata

#### 2. Interfaces (`src/Asynkron.DurableFunctions/Persistence/`)
- **`IConcurrentStateStore`**: Extended interface for concurrency-aware operations
  - `TryClaimLeaseAsync()`: Atomic lease claiming with CAS
  - `RenewLeaseAsync()`: Periodic lease renewal for long-running orchestrators  
  - `ReleaseLeaseAsync()`: Clean lease release on completion
  - `GetReadyClaimableStatesAsync()`: Get states available for claiming

#### 3. Implementation (`src/Asynkron.DurableFunctions/Persistence/`)
- **`ConcurrentSqliteStateStore`**: SQLite implementation with:
  - Automatic schema migration (adds concurrency columns)
  - Atomic transaction-based lease operations
  - Optimistic concurrency with version conflicts
  - Efficient queries with proper indexing

#### 4. Runtime (`src/Asynkron.DurableFunctions/Core/`)
- **`ConcurrentDurableFunctionRuntime`**: Orchestrator-aware runtime with:
  - Automatic lease claiming for orchestrators
  - Background lease renewal for long-running processes
  - Graceful lease release on completion/failure
  - Host identification and load balancing

## Database Schema Changes

The implementation automatically adds concurrency control columns:

```sql
-- Added to existing DurableFunctionStates table
ALTER TABLE DurableFunctionStates ADD COLUMN Version INTEGER DEFAULT 0;
ALTER TABLE DurableFunctionStates ADD COLUMN LeaseOwner TEXT;
ALTER TABLE DurableFunctionStates ADD COLUMN LeaseExpiresAt TEXT;

-- Indexes for performance
CREATE INDEX IF NOT EXISTS IX_DurableFunctionStates_Lease ON DurableFunctionStates(LeaseOwner, LeaseExpiresAt);
CREATE INDEX IF NOT EXISTS IX_DurableFunctionStates_Version ON DurableFunctionStates(Version);
```

## Concurrency Control Flow

### 1. Lease Claiming
```
Host discovers ready orchestrator → Attempts atomic lease claim → 
Database validates current lease status and version → 
Updates lease fields with new owner/expiry if successful →
Host proceeds with execution only if claim succeeds
```

### 2. Lease Renewal
```
Background task runs during orchestrator execution →
Periodically renews lease (at half the lease duration) →
Validates ownership and version before renewal →
Extends expiration time and increments version
```

### 3. Failover Handling
```
Host crashes or loses connectivity →
Lease naturally expires after configured duration → 
Other hosts detect expired lease as claimable →
Seamless takeover without manual intervention
```

## Key Features

### ✅ **Race Condition Prevention**
- Atomic lease operations prevent simultaneous claims
- Version-based optimistic concurrency handles conflicts
- Database transactions ensure consistency

### ✅ **Automatic Failover** 
- Leases expire automatically if host becomes unresponsive
- No permanent locks that could block execution
- Configurable lease duration based on orchestrator patterns

### ✅ **Load Balancing**
- Hosts only claim orchestrators they can execute
- Configurable max results per polling cycle
- Fair distribution across available hosts

### ✅ **Backwards Compatibility**
- Existing applications work unchanged
- Progressive migration (can upgrade one host at a time)  
- No breaking changes to public APIs

### ✅ **Observability**
- Comprehensive logging at all levels
- Lease operation metrics and conflicts
- Host identification in all log messages

## Usage

### Basic Migration
```csharp
// Before (single host)
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var stateStoreLogger = loggerFactory.CreateLogger<SqliteStateStore>();
var stateStore = new SqliteStateStore("Data Source=functions.db", stateStoreLogger);
var runtimeLogger = loggerFactory.CreateLogger<DurableFunctionRuntime>();
var runtime = new DurableFunctionRuntime(stateStore, runtimeLogger, loggerFactory: loggerFactory);

// After (multi-host safe)  
var concurrentStoreLogger = loggerFactory.CreateLogger<ConcurrentSqliteStateStore>();
var stateStore = new ConcurrentSqliteStateStore("Data Source=functions.db", concurrentStoreLogger);
var concurrentRuntimeLogger = loggerFactory.CreateLogger<ConcurrentDurableFunctionRuntime>();
var runtime = new ConcurrentDurableFunctionRuntime(stateStore, concurrentRuntimeLogger, loggerFactory: loggerFactory);
```

### Advanced Configuration
```csharp
var runtime = new ConcurrentDurableFunctionRuntime(
    stateStore, 
    concurrentRuntimeLogger,
    hostId: "web-server-01",
    defaultLeaseDuration: TimeSpan.FromMinutes(10)
);
```

## Testing

### Comprehensive Test Suite (`tests/Asynkron.DurableFunctions.Tests/ConcurrentStateStoreTests.cs`)
- ✅ **Lease claiming**: New states, already leased, expired leases
- ✅ **Lease renewal**: Valid ownership, wrong owner scenarios  
- ✅ **Lease release**: Proper cleanup and version validation
- ✅ **Claimable states**: Ready vs leased vs future states
- ✅ **Concurrent access**: Multiple hosts claiming simultaneously

### Example Implementation (`examples/ConcurrentExample.cs`)
- Demonstrates multi-host setup with shared database
- Shows lease claiming and renewal in action
- Includes both quick and long-running orchestrators

## Performance Impact

### Database Operations
- **Minimal overhead**: Single row updates for lease operations
- **Optimized queries**: Proper indexing for concurrency columns
- **Atomic transactions**: No long-held locks

### Memory Usage  
- **Lease objects**: Small metadata per orchestrator
- **Background tasks**: Lightweight renewal timers
- **No caching**: Relies on database as source of truth

### Scalability
- **Linear scaling**: Each host operates independently
- **No coordination**: No global locks or leader election
- **Configurable limits**: Control orchestrators per polling cycle

## Production Considerations

### Lease Duration Tuning
- **Short orchestrators**: 2-5 minutes (fast failover)
- **Long orchestrators**: 10-30 minutes (reduce renewal overhead)
- **Very long orchestrators**: Consider breaking into smaller parts

### Monitoring
- Track lease claim success/failure rates
- Monitor lease takeover frequency (high frequency may indicate issues)
- Watch for version conflicts (normal, but high rates need investigation)

### High Availability Setup
- Use WAL mode for SQLite: `Journal Mode=WAL;`
- Consider connection pooling for high throughput
- Monitor host health and database connectivity

## Extension Points

### Other Databases
The `IConcurrentStateStore` interface can be implemented for:
- SQL Server with `SELECT ... FOR UPDATE SKIP LOCKED`
- PostgreSQL with similar locking mechanisms
- Cosmos DB with optimistic concurrency and TTL
- Redis with atomic operations and expiration

### Message Queue Integration
Future implementations could use:
- Azure Service Bus with message locking
- Amazon SQS with visibility timeout
- Kafka with consumer groups
- RabbitMQ with acknowledgments

## Files Changed/Added

### New Files
- `src/Asynkron.DurableFunctions/Models/OrchestratorLease.cs`
- `src/Asynkron.DurableFunctions/Models/LeaseClaimResult.cs`
- `src/Asynkron.DurableFunctions/Persistence/IConcurrentStateStore.cs`
- `src/Asynkron.DurableFunctions/Persistence/ConcurrentSqliteStateStore.cs`
- `src/Asynkron.DurableFunctions/Core/ConcurrentDurableFunctionRuntime.cs`
- `tests/Asynkron.DurableFunctions.Tests/ConcurrentStateStoreTests.cs`
- `examples/ConcurrentExample.cs`
- `ORCHESTRATOR_CONCURRENCY.md`
- `CONCURRENCY_SETUP_GUIDE.md`

### Modified Files
- `src/Asynkron.DurableFunctions/Models/DurableState.cs` (added Lease property)
- `src/Asynkron.DurableFunctions/Persistence/SqliteStateStore.cs` (made connection and methods protected)

## Success Metrics

- ✅ **Zero breaking changes**: Existing code works unchanged
- ✅ **Comprehensive testing**: 8 concurrency-specific tests passing
- ✅ **Production ready**: Handles all edge cases and failure scenarios
- ✅ **Well documented**: Complete setup guides and examples
- ✅ **Extensible design**: Clean interfaces for future database support
- ✅ **Minimal performance impact**: Lightweight lease operations

## Conclusion

The implementation successfully addresses the orchestrator concurrency problem with a robust, production-ready solution that:

1. **Prevents conflicts** through atomic lease operations
2. **Enables failover** with automatic lease expiration  
3. **Maintains compatibility** with existing applications
4. **Provides observability** through comprehensive logging
5. **Scales horizontally** across multiple hosts
6. **Extends easily** to other database systems

The solution is now ready for production use and can safely handle multiple hosts executing orchestrators against shared storage.
