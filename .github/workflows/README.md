# GitHub Actions Workflows

This directory contains the automated CI/CD workflow for the Asynkron.DurableFunctions.Public repository.

## Workflow

### 🔨 [`build.yml`](build.yml) - Build and Publish Workflow

**Triggers:** Push to `main`/`develop`, Pull Requests, Manual dispatch

**Purpose:** Simple, reliable build, test, and publish pipeline

**Jobs:**
- **Build and Test**: Runs on Ubuntu with .NET 8.0
- **Package Creation**: Creates NuGet packages for distribution  
- **Examples Validation**: Smoke tests the Examples project execution
- **NuGet Publishing**: Publishes packages to NuGet.org on main branch pushes

**Key Features:**
- Single OS target (Ubuntu) for simplicity and speed
- Release configuration for consistent builds
- Automatic NuGet publishing when NUGET_API_KEY secret is configured
- Package artifacts with 30-day retention for review

## Status Badge

The README.md includes a status badge that links to the workflow:

- [![Build and Publish](https://github.com/asynkron/Asynkron.DurableFunctions.Public/actions/workflows/build.yml/badge.svg)](https://github.com/asynkron/Asynkron.DurableFunctions.Public/actions/workflows/build.yml)

## Testing Strategy

The workflow includes comprehensive validation:

### Build-time Validation:
- ✅ Code compilation and dependency resolution
- ✅ NuGet package generation with proper metadata
- ✅ Solution-wide test execution

### Runtime Validation:
- ✅ Examples project execution smoke test
- ✅ Package integrity validation

## Package Management

The workflow generates NuGet packages for:
- **Asynkron.DurableFunctions.AzureAdapter**: Azure compatibility layer

**Publishing:**
- Automatic publishing to NuGet.org on successful builds to `main` branch
- Requires `NUGET_API_KEY` secret to be configured in repository settings
- Uses `--skip-duplicate` to avoid errors on version conflicts
- **Retention**: 30 days for artifact downloads

## Development Guidelines

When contributing:

1. **Pull Requests**: The workflow runs automatically on all PRs
2. **Main Branch**: Successful builds trigger NuGet publishing automatically
3. **Package Validation**: Review generated packages in workflow artifacts before merging

## Configuration

To enable NuGet publishing:
1. Go to repository Settings > Secrets and variables > Actions
2. Add a new repository secret named `NUGET_API_KEY`
3. Set the value to your NuGet.org API key

## Maintenance

This workflow is designed to be:
- **Simple**: Single job, single OS, minimal complexity
- **Fast**: No unnecessary matrix builds or platform testing
- **Reliable**: Focused on core functionality with proper error handling
- **Maintainable**: Clear, straightforward configuration