# 🐘 PostgreSQL Setup Guide for Asynkron.DurableFunctions

This guide shows you how to set up and use PostgreSQL as the state store for Asynkron.DurableFunctions.

## 🚀 Quick Start with Docker Compose

The easiest way to get started is using the provided Docker Compose configuration.

### 1. Start PostgreSQL and PgAdmin

```bash
# Start the services
docker-compose up -d

# Check that services are running
docker-compose ps
```

This will start:
- **PostgreSQL 16** on port `5432`
- **PgAdmin 4** on port `5050` (optional web interface)

### 2. Verify Database Connection

The database will be automatically initialized with the required schema. You can connect using:

- **Host**: `localhost`
- **Port**: `5432`
- **Database**: `durablefunctions`
- **Username**: `durableuser`
- **Password**: `durablepass`

### 3. Use PgAdmin (Optional)

Access PgAdmin at [http://localhost:5050](http://localhost:5050):
- **Email**: `admin@durablefunctions.com`
- **Password**: `admin123`

## 💻 Using PostgreSQL in Your Application

### Option 1: Direct Usage

```csharp
using Asynkron.DurableFunctions.Core;
using Asynkron.DurableFunctions.Persistence;
using Microsoft.Extensions.Logging;

// Create logger factory
using var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// Connection string
var connectionString = "Host=localhost;Port=5432;Database=durablefunctions;Username=durableuser;Password=durablepass;Include Error Detail=true;";

// Create PostgreSQL state store
var stateStore = new PostgreSqlStateStore(connectionString, 
    loggerFactory.CreateLogger<PostgreSqlStateStore>());

// Create runtime
var runtime = new DurableFunctionRuntime(
    stateStore,
    loggerFactory.CreateLogger<DurableFunctionRuntime>(),
    loggerFactory: loggerFactory);

// Register your functions and orchestrators
runtime.RegisterFunction<string, string>("MyFunction", async input =>
{
    return $"Processed: {input}";
});

// Start the runtime
await runtime.StartAsync(CancellationToken.None);
```

### Option 2: ASP.NET Core Dependency Injection

```csharp
using Asynkron.DurableFunctions.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Get connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("PostgreSQL") 
    ?? "Host=localhost;Port=5432;Database=durablefunctions;Username=durableuser;Password=durablepass;Include Error Detail=true;";

// Add Durable Functions with PostgreSQL
builder.Services.AddDurableFunctionsWithPostgreSQL(connectionString, options =>
{
    options.PollingIntervalMs = 100;
});

// Add management services (optional)
builder.Services.AddDurableFunctionsManagement();

var app = builder.Build();

// Configure the pipeline
app.UseRouting();
app.MapControllers();

// Register your functions
var runtime = app.Services.GetRequiredService<DurableFunctionRuntime>();
runtime.RegisterFunction<string, string>("MyFunction", async input =>
{
    return $"Processed: {input}";
});

await app.RunAsync();
```

### Option 3: Using Configuration

Add to your `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=durablefunctions;Username=durableuser;Password=durablepass;Include Error Detail=true;"
  },
  "DurableFunctions": {
    "PollingIntervalMs": 100,
    "LeaseDurationMinutes": 5
  }
}
```

## 🔧 Configuration Options

### Connection String Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `Host` | PostgreSQL server host | `localhost` |
| `Port` | PostgreSQL server port | `5432` |
| `Database` | Database name | `durablefunctions` |
| `Username` | Database username | `durableuser` |
| `Password` | Database password | `durablepass` |
| `Include Error Detail` | Include detailed error info | `true` |
| `Timeout` | Connection timeout (seconds) | `15` |
| `CommandTimeout` | Command timeout (seconds) | `30` |
| `Pooling` | Enable connection pooling | `true` |
| `MinPoolSize` | Minimum pool size | `0` |
| `MaxPoolSize` | Maximum pool size | `100` |

### Runtime Options

```csharp
builder.Services.AddDurableFunctionsWithPostgreSQL(connectionString, options =>
{
    options.PollingIntervalMs = 100;        // How often to poll for work (ms)
    options.MaxConcurrentWorkflows = 10;    // Max concurrent orchestrators
    options.EnableReplayLogging = false;    // Enable replay-safe logging
});
```

## 🗄️ Database Schema

The database schema is automatically created by the initialization script. Here's what gets created:

### Main Table: `durable_state`

| Column | Type | Description |
|--------|------|-------------|
| `instance_id` | VARCHAR(255) PRIMARY KEY | Unique instance identifier |
| `state_json` | JSONB NOT NULL | Serialized `DurableStateDto` payload (includes input, history, metadata) |
| `function_name` | VARCHAR(255) NOT NULL | Logical function/orchestrator name |
| `execute_after` | TIMESTAMPTZ NOT NULL | Scheduler timestamp for next execution |
| `is_completed` | BOOLEAN NOT NULL | Completion flag used by cleanup and queries |
| `lease_owner` | VARCHAR(255) | Host that currently owns the orchestration lease |
| `lease_expires_at` | TIMESTAMPTZ | Lease expiration instant |
| `version` | BIGINT NOT NULL | Optimistic concurrency token |
| `created_at` | TIMESTAMPTZ NOT NULL | Row creation timestamp |
| `updated_at` | TIMESTAMPTZ NOT NULL | Last modification timestamp (maintained by trigger) |

> Existing databases created before vNext may still include legacy columns such as `input` or `entries`.
> They are harmless and will be ignored by the current runtime; new deployments only rely on `state_json`.

### Indexes

- `idx_durable_state_execute_after` - Query ready-to-execute instances
- `idx_durable_state_function_name` - Query by function name
- `idx_durable_state_lease` - Lease management queries
- `idx_durable_state_completed` - Query completed instances

## 🔄 Production Considerations

### 1. Connection Pooling

PostgreSQL handles connection pooling well. Configure pool size based on your needs:

```csharp
var connectionString = "Host=localhost;Port=5432;Database=durablefunctions;Username=durableuser;Password=durablepass;Pooling=true;MinPoolSize=5;MaxPoolSize=50;";
```

### 2. High Availability

For production, consider:
- PostgreSQL clustering or replication
- Connection string with multiple hosts
- Proper backup strategies

### 3. Performance Tuning

- Monitor index usage with `EXPLAIN ANALYZE`
- Adjust `work_mem` and `shared_buffers` in PostgreSQL
- Consider partitioning for large tables
- Regular `VACUUM` and `ANALYZE`

### 4. Monitoring

Monitor these metrics:
- Active connections
- Query execution times
- Lease contention
- Table size and growth

## 🧹 Maintenance

### Cleanup Completed Instances

```sql
-- Delete completed instances older than 7 days
DELETE FROM durable_state 
WHERE is_completed = true 
AND updated_at < NOW() - INTERVAL '7 days';
```

### Monitor Lease Health

```sql
-- Check for stuck leases
SELECT instance_id, function_name, lease_owner, lease_expires_at
FROM durable_state 
WHERE lease_owner IS NOT NULL 
AND lease_expires_at < NOW()
AND NOT is_completed;
```

## 🐳 Docker Commands

```bash
# Start services
docker-compose up -d

# View logs
docker-compose logs -f postgres
docker-compose logs -f pgadmin

# Stop services
docker-compose down

# Stop and remove volumes (⚠️ deletes data)
docker-compose down -v

# Connect to PostgreSQL directly
docker exec -it asynkron-postgres psql -U durableuser -d durablefunctions
```

## 🔍 Troubleshooting

### Connection Issues

1. **Check if PostgreSQL is running**:
   ```bash
   docker-compose ps
   ```

2. **Check logs**:
   ```bash
   docker-compose logs postgres
   ```

3. **Test connection**:
   ```bash
   docker exec -it asynkron-postgres pg_isready -U durableuser -d durablefunctions
   ```

### Permission Issues

Make sure the user has proper permissions:
```sql
GRANT ALL PRIVILEGES ON TABLE durable_state TO durableuser;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO durableuser;
```

### Performance Issues

1. **Check active connections**:
   ```sql
   SELECT count(*) FROM pg_stat_activity WHERE datname = 'durablefunctions';
   ```

2. **Monitor query performance**:
   ```sql
   SELECT query, mean_time, calls FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;
   ```

## 📚 Resources

- [Npgsql Documentation](https://www.npgsql.org/doc/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Docker Compose Reference](https://docs.docker.com/compose/)