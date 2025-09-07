# NetDaemon3Automations Agent Guidelines

## Build Commands
- **Build**: `dotnet build`
- **Build Release**: `dotnet build --configuration Release`
- **Restore**: `dotnet restore`

## Test Commands
- **Run all tests**: `dotnet test`
- **Run single test**: `dotnet test --filter "FullyQualifiedName~TestClass.TestMethod"`
- **Run with coverage**: `dotnet test --collect:"XPlat Code Coverage"`

## Code Style Guidelines

### Language & Framework
- **Target Framework**: .NET 9.0
- **Language Version**: C# 12.0
- **Nullable**: Enable nullable reference types
- **Reactive Extensions**: Use System.Reactive.Linq for event handling

### Naming Conventions
- **Classes**: PascalCase (e.g., `LightManager`, `LightsConfiguration`)
- **Methods**: PascalCase (e.g., `Initialize()`, `TurnOnAmbient()`)
- **Properties**: PascalCase (e.g., `IsNight`, `CancellationToken`)
- **Private fields**: camelCase with underscore prefix (e.g., `_config`, `_logger`)
- **Constants**: PascalCase

### Imports & Usings
- Use global usings in `GlobalUsings.cs` and `Daemon3Usings.cs`
- Group usings: System.*, then third-party, then project-specific
- Remove unused usings

### Patterns & Architecture
- **Dependency Injection**: Constructor injection with `ILogger<T>`, `IScheduler`, `IHaContext`
- **NetDaemon Apps**: Decorate with `[NetDaemonApp]` attribute
- **Async Initialization**: Implement `IAsyncInitializable` when needed
- **Configuration**: Use `IAppConfig<T>` for app-specific settings
- **Entity Access**: Use `Entities` and `Services` classes from HassModel

### Error Handling
- Use nullable checks with `?.` operator
- Handle cancellation tokens in async operations
- Log errors using `ILogger`

### Testing
- **Framework**: xUnit with `Fact` and `Theory`
- **Mocking**: NSubstitute (preferred) or Moq
- **Assertions**: FluentAssertions
- **Test Structure**: Arrange-Act-Assert pattern
- **Test Naming**: `MethodName_ShouldExpectedBehavior_WhenCondition`

### Code Generation
- **Home Assistant entities**: `dotnet tool run nd-codegen` to generate `HomeAssistantGenerated.cs`</content>
<parameter name="filePath">/home/thhel/git/NetDaemon3Automations/AGENTS.md
