# Helto NetDaemon Automations

Personal Home Assistant automations built with NetDaemon 26.45.0-alpha-4, .NET 11, C# 14, and Reactive Extensions.

## Prerequisites

- .NET SDK from `global.json`
- PowerShell 7 for deployment
- A Home Assistant long-lived access token for code generation

## First-time setup

```bash
cp appsettings.example.json appsettings.json
dotnet tool restore
dotnet tool run nd-codegen -o src/NetDaemonApps/HomeAssistantGenerated.cs -ns HomeAssistantGenerated
dotnet restore NetDaemonApps.slnx --locked-mode -m:1
dotnet build NetDaemonApps.slnx --no-restore
dotnet test NetDaemonApps.slnx --no-build --no-restore
```

Add the real Home Assistant host and token to `appsettings.json`. All real appsettings files are ignored by Git.

The generated Home Assistant model is checked in so builds and CI do not need access to the private Home Assistant instance. Regenerate it whenever entities, services, or NetDaemon versions change. See the [official NetDaemon codegen guide](https://netdaemon.xyz/docs/user/hass_model/hass_model_codegen/).

## Dependency updates

Update the entire solution, synchronize the code generator, and refresh the generated model:

```bash
pwsh ./update_all_dependencies.ps1
```

The script uses `dotnet outdated -u --recursive <repository-root>` so both production and test projects are updated in one repository-wide pass.

## Project structure

- `Domain/` contains pure, testable automation policies and Home Assistant vocabulary.
- `Infrastructure/` owns subscription lifetimes, command sequencing, and configuration validation.
- `apps/netdaemon3/` contains thin NetDaemon adapters and their YAML configuration.
- `NetDaemonApps.Tests/` tests policies and observable behavior through public seams.

## Deploy

See [Deploy.md](Deploy.md).
