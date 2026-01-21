# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

.NET 10.0 Worker Service for copying files to Azure Storage Account. Currently a template with the core hosting infrastructure set up but the actual file copying functionality not yet implemented.

## Build Commands

```bash
dotnet build          # Build the project
dotnet run            # Run the worker service
dotnet publish        # Create deployment package
```

## Architecture

- **Program.cs** - Entry point that configures dependency injection and registers the Worker as a hosted service
- **Worker.cs** - Background service implementing `BackgroundService`, runs continuously until cancelled

The project uses the .NET Generic Host pattern with `Microsoft.Extensions.Hosting` for lifecycle management and dependency injection.

## Configuration

- `appsettings.json` / `appsettings.Development.json` - Environment-specific settings
- User Secrets enabled for sensitive configuration (connection strings, keys)
