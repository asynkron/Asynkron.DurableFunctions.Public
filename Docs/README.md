# Documentation

This directory contains documentation configuration and generated documentation files.

## DocFX Configuration

The `docfx.json` file configures automatic API documentation generation from XML comments.

## Building Documentation

To build the documentation:

```bash
# Install DocFX if not already installed
dotnet tool install -g docfx

# Build documentation
docfx docs/docfx.json
```

The generated documentation will be available in the `docs/_site` directory.