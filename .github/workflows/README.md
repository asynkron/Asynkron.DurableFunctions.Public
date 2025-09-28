# GitHub Actions Workflows

This directory contains automated CI/CD workflows for the Asynkron.DurableFunctions.Public repository.

## Workflows

### 🔨 [`build.yml`](build.yml) - Main Build Workflow

**Triggers:** Push to `main`/`develop`, Pull Requests, Manual dispatch

**Purpose:** Comprehensive build and test across multiple platforms

**Jobs:**
- **Multi-platform Build Matrix**: Tests on Ubuntu, Windows, and macOS
- **Multi-configuration**: Builds both Debug and Release configurations  
- **Solution-wide Testing**: Runs all tests in the solution
- **Examples Validation**: Smoke tests the Examples project execution

**Artifacts:** NuGet packages (on main branch only)

### ⚡ [`azure-adapter.yml`](azure-adapter.yml) - Azure Adapter Focused Build

**Triggers:** 
- Changes to `src/Asynkron.DurableFunctions.AzureAdapter/**`
- Changes to this workflow file
- Manual dispatch

**Purpose:** Specialized validation for the Azure Adapter package

**Jobs:**
1. **Build AzureAdapter**: 
   - Builds in Debug and Release configurations
   - Generates and validates NuGet packages
   - Verifies XML documentation generation
   - Validates package contents

2. **Integration Testing**:
   - Tests full solution build with AzureAdapter
   - Validates Azure compatibility examples
   - Ensures project references work correctly

**Artifacts:** Azure Adapter NuGet packages (30-day retention)

## Status Badges

The README.md includes status badges that link to these workflows:

- [![Build and Test](https://github.com/asynkron/Asynkron.DurableFunctions.Public/actions/workflows/build.yml/badge.svg)](https://github.com/asynkron/Asynkron.DurableFunctions.Public/actions/workflows/build.yml)
- [![Azure Adapter Build](https://github.com/asynkron/Asynkron.DurableFunctions.Public/actions/workflows/azure-adapter.yml/badge.svg)](https://github.com/asynkron/Asynkron.DurableFunctions.Public/actions/workflows/azure-adapter.yml)

## Testing Strategy

The workflows include both build-time and runtime validation:

### Build-time Validation:
- ✅ Code compilation across platforms
- ✅ NuGet package generation  
- ✅ XML documentation generation
- ✅ Project reference resolution

### Runtime Validation:
- ✅ Smoke tests via dedicated test project
- ✅ Examples project execution tests
- ✅ Azure compatibility verification

## Package Management

Both workflows generate NuGet packages as artifacts:

- **Main workflow**: Packages on successful builds to `main` branch
- **Azure Adapter workflow**: Always generates packages for validation
- **Retention**: 7 days (main), 30 days (Azure Adapter)
- **Naming**: Descriptive artifact names for easy identification

## Development Guidelines

When contributing:

1. **Pull Requests**: All workflows run automatically on PRs
2. **Azure Adapter Changes**: Trigger the specialized Azure Adapter workflow  
3. **Breaking Changes**: Ensure all platforms pass before merging
4. **Package Validation**: Review generated packages in workflow artifacts

## Maintenance

These workflows are designed to be:
- **Self-contained**: No external dependencies beyond GitHub Actions marketplace
- **Efficient**: Smart triggering to avoid unnecessary runs
- **Reliable**: Comprehensive validation across scenarios
- **Maintainable**: Clear separation of concerns between workflows