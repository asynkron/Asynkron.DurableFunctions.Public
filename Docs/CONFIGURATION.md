# Configuration Reference

This document provides comprehensive configuration options for Asynkron Durable Functions with validation support.

## Overview

Durable Functions supports comprehensive configuration validation at startup to catch configuration issues early. Configuration validation includes:

- **Storage Configuration**: Connection string validation, connectivity testing
- **Runtime Configuration**: Host ID validation, timeout validation, concurrency limits  
- **Security Configuration**: Input size limits, serialization depth, database security

## Basic Usage

### Using Configuration Validation

```csharp
// Program.cs
using Asynkron.DurableFunctions.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services with comprehensive validation
builder.Services.AddDurableFunctions(builder.Configuration);

var app = builder.Build();
```

### Configuration File (appsettings.json)

```json
{
  "DurableFunctions": {
    "Storage": {
      "Provider": "PostgreSQL",
      "PostgreSQL": {
        "ConnectionString": "Host=localhost;Database=durablefunctions;Username=user;Password=pass;",
        "RequireSsl": true,
        "ValidateConnectivity": false
      }
    },
    "Runtime": {
      "HostId": "my-app-instance-1",
      "OrchestrationTimeout": "00:30:00",
      "LeaseRenewalInterval": "00:00:10",
      "LeaseTimeout": "00:00:30",
      "PollingInterval": "00:00:00.100",
      "MaxConcurrentOrchestrations": 100
    },
    "Security": {
      "MaxInputSize": 65536,
      "MaxSerializationDepth": 32,
      "ValidateInputs": true,
      "Database": {
        "RequireSsl": true,
        "ValidateCertificates": true,
        "CommandTimeoutSeconds": 30
      }
    }
  }
}
```

## Storage Configuration

### PostgreSQL Provider

```json
{
  "DurableFunctions": {
    "Storage": {
      "Provider": "PostgreSQL",
      "PostgreSQL": {
        "ConnectionString": "Host=localhost;Port=5432;Database=durablefunctions;Username=user;Password=pass;SSL Mode=Require;",
        "RequireSsl": true,
        "ValidateConnectivity": false
      }
    }
  }
}
```

**PostgreSQL Options:**
- `ConnectionString` (required): Full PostgreSQL connection string
- `RequireSsl` (default: true): Require SSL connections
- `ValidateConnectivity` (default: false): Test database connectivity during validation

**Connection String Requirements:**
- Must include `Host` parameter
- Must include `Database` parameter
- Should include authentication (`Username`, `Password` or integrated auth)
- SSL Mode should match `RequireSsl` setting

### SQLite Provider

```json
{
  "DurableFunctions": {
    "Storage": {
      "Provider": "SQLite",
      "SQLite": {
        "ConnectionString": "Data Source=durablefunctions.db;Cache=Shared;"
      }
    }
  }
}
```

**SQLite Options:**
- `ConnectionString` (required): SQLite connection string with Data Source

**Connection String Requirements:**
- Must include `Data Source` parameter
- Use `:memory:` for in-memory databases
- File paths are validated for directory existence

### In-Memory Provider

```json
{
  "DurableFunctions": {
    "Storage": {
      "Provider": "InMemory"
    }
  }
}
```

**Use Cases:**
- Development and testing
- Temporary orchestrations
- Single-instance deployments

## Runtime Configuration

```json
{
  "DurableFunctions": {
    "Runtime": {
      "HostId": "my-service-prod-01",
      "OrchestrationTimeout": "01:00:00",
      "LeaseRenewalInterval": "00:00:10",
      "LeaseTimeout": "00:00:30",
      "PollingInterval": "00:00:00.100",
      "MaxConcurrentOrchestrations": 200
    }
  }
}
```

**Runtime Options:**

- `HostId` (required): Unique identifier for this host
  - Maximum 255 characters
  - Alphanumeric, dash, and underscore only
  - Must be unique across all instances

- `OrchestrationTimeout` (default: "00:30:00"): Maximum orchestration runtime
  - TimeSpan format: `[days.]hours:minutes:seconds[.fractional]`
  - Must be positive
  - Warning if > 30 days

- `LeaseRenewalInterval` (default: "00:00:10"): Lease renewal frequency
  - Must be positive
  - Must be less than `LeaseTimeout`

- `LeaseTimeout` (default: "00:00:30"): Lease validity period
  - Must be positive
  - Must be greater than `LeaseRenewalInterval`

- `PollingInterval` (default: "00:00:00.100"): Work polling frequency
  - Must be positive
  - Warning if < 100ms (may cause high CPU usage)

- `MaxConcurrentOrchestrations` (default: 100): Maximum concurrent orchestrations
  - Must be positive
  - Warning if > 10,000 (may cause resource issues)

## Security Configuration

```json
{
  "DurableFunctions": {
    "Security": {
      "MaxInputSize": 65536,
      "MaxSerializationDepth": 32,
      "ValidateInputs": true,
      "Database": {
        "RequireSsl": true,
        "ValidateCertificates": true,
        "CommandTimeoutSeconds": 30
      }
    }
  }
}
```

**Security Options:**

- `MaxInputSize` (default: 65536): Maximum input size in bytes
  - Must be positive
  - Warning if > 100MB

- `MaxSerializationDepth` (default: 32): Maximum JSON nesting depth
  - Must be positive
  - Warning if > 1000 (stack overflow risk)

- `ValidateInputs` (default: true): Enable input type validation
  - Warning if enabled but no allowed types specified

- `Database`: Database security settings
  - `RequireSsl` (default: true): Require SSL connections
  - `ValidateCertificates` (default: true): Validate SSL certificates
  - `CommandTimeoutSeconds` (default: 30): Database command timeout

## Validation Behavior

### Error Levels

- **Error**: Configuration is invalid, application will not start
- **Warning**: Configuration may cause issues, logged but allows startup
- **Info**: Informational messages about configuration choices

### Validation Process

1. **Startup Validation**: Complete validation during `AddDurableFunctions()`
2. **Health Check**: Ongoing validation via `ConfigurationValidationHealthCheck`
3. **Logging**: All validation messages logged with appropriate levels

### Error Handling

```csharp
try 
{
    builder.Services.AddDurableFunctions(builder.Configuration);
}
catch (ConfigurationException ex)
{
    // Handle configuration validation errors
    Console.WriteLine($"Configuration error: {ex.Message}");
    Environment.Exit(1);
}
```

## Health Checks

Add configuration validation to health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<ConfigurationValidationHealthCheck>("configuration");
```

Health check results:
- **Healthy**: No validation errors or warnings
- **Degraded**: Configuration has warnings
- **Unhealthy**: Configuration has errors

## Best Practices

### Development Environment
```json
{
  "DurableFunctions": {
    "Storage": {
      "Provider": "InMemory"
    },
    "Runtime": {
      "HostId": "dev-local",
      "PollingInterval": "00:00:01",
      "MaxConcurrentOrchestrations": 10
    }
  }
}
```

### Production Environment
```json
{
  "DurableFunctions": {
    "Storage": {
      "Provider": "PostgreSQL",
      "PostgreSQL": {
        "ConnectionString": "Host=prod-db;Database=durablefunctions;Username=service_user;Password=${DB_PASSWORD};SSL Mode=Require;",
        "RequireSsl": true,
        "ValidateConnectivity": false
      }
    },
    "Runtime": {
      "HostId": "${HOSTNAME}",
      "OrchestrationTimeout": "01:00:00",
      "MaxConcurrentOrchestrations": 500
    },
    "Security": {
      "MaxInputSize": 1048576,
      "Database": {
        "RequireSsl": true,
        "ValidateCertificates": true,
        "CommandTimeoutSeconds": 60
      }
    }
  }
}
```

### Recommendations

1. **Always use unique HostId values** in multi-instance deployments
2. **Enable SSL in production** for database connections
3. **Set appropriate timeouts** based on your orchestration complexity
4. **Monitor configuration health checks** for ongoing validation
5. **Use environment variables** for sensitive configuration values
6. **Test configuration validation** in CI/CD pipelines

## Schema Support

IDE support is available through the JSON schema:

```json
{
  "$schema": "./schemas/durablefunctions-config.json",
  "DurableFunctions": {
    // Configuration with IntelliSense support
  }
}
```

## Migration Guide

### From Legacy Configuration

If you're using the legacy service registration methods:

```csharp
// Old way
services.AddDurableFunctionsWithPostgreSQL(connectionString);

// New way with validation  
services.AddDurableFunctions(configuration);
```

Update your `appsettings.json` to use the new structured format shown in this document.

### Validation Messages

Common validation messages and their solutions:

- **"HostId is required"**: Set a unique `Runtime.HostId` value
- **"PostgreSQL connection string is required"**: Provide `Storage.PostgreSQL.ConnectionString`
- **"LeaseRenewalInterval must be less than LeaseTimeout"**: Adjust timing configuration
- **"SSL is disabled but RequireSsl is true"**: Match SSL settings in connection string and configuration

For additional support, see the project repository and documentation.