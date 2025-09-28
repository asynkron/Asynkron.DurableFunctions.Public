# Orchestrator Concurrency Across Multiple Hosts

This document describes the concurrency control features that ensure safe orchestrator execution when multiple hosts may pick up the same DurableState instance.

## Overview

The concurrency control system prevents multiple hosts from executing the same orchestration instance simultaneously, which could lead to duplicate or concurrent execution, violating single-active guarantees.

## Key Features

### 1. Optimistic Concurrency via Compare-And-Swap (CAS)
- Each orchestrator state has a version field that increments with each modification
- Only the host whose update succeeds with the correct version can proceed with execution
- Other hosts back off and retry as needed

### 2. Short TTL Lease Field
- Hosts claim orchestrator instances by setting a lease_owner and lease_expires_at field
- If the lease expires (due to host crash/network partition), another host can claim it
- Hosts renew leases periodically during execution

### 3. Automatic Failover
- When a host crashes or loses connectivity, its leases will expire
- Other hosts can safely pick up and execute the orphaned orchestrators
- No permanent locks that could block execution indefinitely

## Database Schema Changes

The concurrency system adds the following columns to the DurableFunctionStates table:

```sql
ALTER TABLE DurableFunctionStates ADD COLUMN Version INTEGER DEFAULT 0;
ALTER TABLE DurableFunctionStates ADD COLUMN LeaseOwner TEXT;
ALTER TABLE DurableFunctionStates ADD COLUMN LeaseExpiresAt TEXT;

-- Indexes for efficient concurrency queries
CREATE INDEX IF NOT EXISTS IX_DurableFunctionStates_Lease ON DurableFunctionStates(LeaseOwner, LeaseExpiresAt);
CREATE INDEX IF NOT EXISTS IX_DurableFunctionStates_Version ON DurableFunctionStates(Version);
```

These columns are automatically added when using the `ConcurrentSqliteStateStore`.

## Architecture Components

### Core Models

#### OrchestratorLease
Represents a lease for orchestrator execution:
```csharp
public class OrchestratorLease
{
    public string LeaseOwner { get; set; }        // Host identifier
    public DateTimeOffset LeaseExpiresAt { get; set; }  // Expiration time
    public long Version { get; set; }             // For optimistic concurrency
}
```

#### LeaseClaimResult
Result of attempting to claim a lease:
```csharp
public class LeaseClaimResult
{
    public bool Success { get; set; }
    public DurableState? State { get; set; }
    public OrchestratorLease? Lease { get; set; }
    public string? FailureReason { get; set; }
}
```

### Interfaces

#### IConcurrentStateStore
Extended interface for concurrency-aware state stores:
```csharp
public interface IConcurrentStateStore : IStateStore
{
    Task<LeaseClaimResult> TryClaimLeaseAsync(string instanceId, string leaseOwner, TimeSpan leaseDuration, DateTimeOffset? currentTime = null);
    Task<bool> RenewLeaseAsync(string instanceId, string leaseOwner, TimeSpan leaseDuration, long expectedVersion);
    Task<bool> ReleaseLeaseAsync(string instanceId, string leaseOwner, long expectedVersion);
    Task<IEnumerable<DurableState>> GetReadyClaimableStatesAsync(DateTimeOffset currentTime, string leaseOwner, int maxResults = int.MaxValue);
}
```

### Implementations

#### ConcurrentSqliteStateStore
SQLite implementation with concurrency control:
- Extends the base SqliteStateStore
- Adds concurrency columns automatically
- Implements lease claiming, renewal, and release operations
- Provides atomic lease operations using database transactions

#### ConcurrentDurableFunctionRuntime
Runtime wrapper with concurrency awareness:
- Wraps the standard DurableFunctionRuntime
- Implements lease-based orchestrator execution
- Provides automatic lease renewal for long-running orchestrators
- Handles graceful lease release on completion or failure

## Concurrency Control Flow

### 1. Lease Claiming Process
```
1. Host discovers ready orchestrator state
2. Host attempts to claim lease using TryClaimLeaseAsync()
3. Database checks current lease status and version
4. If claimable, database atomically updates lease fields
5. Host receives success/failure result
6. Only successful host proceeds with execution
```

### 2. Lease Renewal Process
```
1. During orchestrator execution, renewal task runs periodically
2. Renewal occurs at half the lease duration interval
3. Database validates owner and version before renewal
4. Lease expiration time is extended
5. Version is incremented for next operations
```

### 3. Lease Release Process
```
1. When orchestrator completes (success or failure)
2. Host calls ReleaseLeaseAsync() with current version
3. Database clears lease fields atomically
4. Other hosts can now claim the state (if needed for retry)
```

## Usage Examples

See [CONCURRENCY_SETUP_GUIDE.md](CONCURRENCY_SETUP_GUIDE.md) for detailed setup and usage examples.

## Optimized Polling Performance

To improve performance when polling for ready orchestrators, the concurrent state store implements a two-step optimization approach:

### Problem
The original polling implementation retrieved and deserialized complete `DurableState` objects, including:
- Complex ActivityResults dictionaries
- HistoryEvents arrays  
- ExternalEvents and AwaitedExternalEvents
- Full input/output data

This was inefficient when only basic scheduling information was needed to determine if an instance could be claimed.

### Solution: Lightweight SchedulableInstance

A new `SchedulableInstance` model contains only the minimal data needed for scheduling decisions:

```csharp
public class SchedulableInstance
{
    public string InstanceId { get; set; }
    public DateTimeOffset ExecuteAfter { get; set; }
    public string FunctionName { get; set; }
    public long Version { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
}
```

### Two-Step Process

1. **Lightweight Polling**: `GetReadyClaimableInstanceIdsAsync()` returns only `SchedulableInstance` objects
2. **Lease Claiming**: Attempt to claim leases for eligible instances  
3. **State Loading**: Only load full `DurableState` after successfully claiming a lease via `GetStateForClaimedInstanceAsync()`

### Performance Benefits

- **Reduced I/O**: Minimal data transfer during polling
- **Faster Deserialization**: Simple objects vs complex state graphs
- **Better Scalability**: Less memory and CPU usage per polling cycle
- **Efficient Filtering**: Quick lease eligibility checks without full state deserialization

### Backward Compatibility

The original `GetReadyClaimableStatesAsync` method remains available for backward compatibility, though the optimized approach is preferred for new implementations.

## Configuration

### Default Settings
- **Lease Duration**: 5 minutes (configurable)
- **Renewal Interval**: 2.5 minutes (half of lease duration)
- **Host ID**: `{MachineName}-{ProcessId}` (configurable)
- **Max Results**: 10 orchestrators per polling cycle (configurable)

### Customization
```csharp
var runtime = new ConcurrentDurableFunctionRuntime(
    concurrentStateStore: stateStore,
    logger: logger,
    hostId: "custom-host-id",
    defaultLeaseDuration: TimeSpan.FromMinutes(10) // Custom lease duration
);
```

## Error Handling

### Common Scenarios

1. **Version Conflicts**: When multiple hosts try to modify the same state simultaneously
   - Result: One succeeds, others get version conflict error
   - Recovery: Other hosts will retry on next polling cycle

2. **Lease Expiration**: When a host crashes or becomes unresponsive
   - Result: Lease expires after configured duration
   - Recovery: Other hosts can claim and execute the orchestrator

3. **Network Partitions**: When a host temporarily loses database connectivity
   - Result: Host cannot renew lease, lease expires
   - Recovery: When connectivity returns, other host may have taken over

### Monitoring and Logging

The system provides comprehensive logging at different levels:
- **Information**: Successful lease operations
- **Warning**: Failed renewals, lease conflicts
- **Error**: Database errors, execution failures
- **Debug**: Detailed operation traces

## Performance Considerations

### Database Impact
- Lease operations add minimal overhead (single row updates)
- Indexes optimize concurrency queries
- Transaction usage ensures atomicity without long locks

### Scalability
- Each host polls independently
- Load balancing through maxResults parameter
- No global coordination required

### Memory Usage
- Minimal additional memory per orchestrator state
- Lease renewal tasks have small footprint
- No persistent connections beyond state store

## Best Practices

1. **Lease Duration**: Set based on typical orchestrator execution time
   - Too short: Frequent renewals, potential takeover during normal execution
   - Too long: Slower failover recovery

2. **Host Identification**: Use stable, unique identifiers
   - Include machine name and process info
   - Avoid changing host IDs frequently

3. **Monitoring**: Track lease operations and conflicts
   - Monitor failed claim attempts
   - Watch for frequent lease takeovers (may indicate issues)

4. **Error Handling**: Implement proper cleanup
   - Always release leases in finally blocks
   - Handle renewal failures gracefully

5. **Testing**: Test concurrency scenarios
   - Multiple hosts with same orchestrators
   - Network partitions and host failures
   - Lease expiration and takeover scenarios