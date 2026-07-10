# NetDaemon Automations Agent Guidelines

## Build and test

- Restore: `dotnet restore NetDaemonApps.slnx --locked-mode -m:1`
- Build: `dotnet build NetDaemonApps.slnx --no-restore`
- Release build: `dotnet build NetDaemonApps.slnx --configuration Release --no-restore`
- Test: `dotnet test NetDaemonApps.slnx --no-build --no-restore`
- Coverage: `dotnet test NetDaemonApps.slnx --collect:"XPlat Code Coverage" --settings coverlet.runsettings`
- Single test: `dotnet test NetDaemonApps.slnx --filter "FullyQualifiedName~TestClass.TestMethod"`

## Platform

- Target framework: .NET 10
- Language version: C# 14
- Nullable reference types: enabled
- Reactive event handling: System.Reactive

## Architecture

- Keep app adapters focused on Home Assistant and NetDaemon orchestration.
- Put reusable, complex, or framework-independent decisions in pure policy modules under `Domain/`.
- Store every subscription and schedule through `ReactiveApp`.
- Initialize reactive apps through `IAsyncInitializable`.
- Use `TimeProvider` or the injected scheduler instead of `DateTime.Now`.
- Use the shared Home Assistant vocabulary and `HouseModeCodec` instead of repeated state strings.
- Validate app configuration at construction time and fail clearly.

## Testing

- Framework: xUnit
- Mocking: NSubstitute
- Assertions: FluentAssertions
- Test public behavior, policy interfaces, and `IHaContext` service calls.
- Use Arrange–Act–Assert and descriptive `Method_ShouldBehavior_WhenCondition` names.

## Generated model

Never hand-edit `src/NetDaemonApps/HomeAssistantGenerated.cs`.

```bash
dotnet tool restore
dotnet tool run nd-codegen -o src/NetDaemonApps/HomeAssistantGenerated.cs -ns HomeAssistantGenerated
```

Keep the local codegen tool and all `NetDaemon.*` packages on the same version. Follow the official guide: https://netdaemon.xyz/docs/user/hass_model/hass_model_codegen/
