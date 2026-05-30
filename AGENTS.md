# Digital Wellbeing for Windows

## Tech Stack
- .NET 9 / C# / WPF with Material Design
- SQLite (sqlite-net-pcl) for local storage
- NAudio for audio monitoring
- LiveCharts for data visualization
- Velopack for auto-updates
- xUnit for testing
- GitHub Actions CI/CD (tag-triggered releases)

## Architecture
- MVVM: `Views/`, `ViewModels/`, `Models/`
- Services layer: `Services/` (business logic, data access, platform services)
- Core tracking: `CoreLogic/` (AppUsageTracker, ScreenTimeTracker, SoundExposureManager)
- Platform-specific: `Platform/Windows/` (native interop, idle detection, focus changes)
- WPF converters: `Converters/`
- Tests: `Tests/Tests.csproj` (nested xUnit project)
- Solution: `DigitalWellbeing.sln` (main app + installer)

## Commands
```powershell
dotnet build
dotnet run --project digital-wellbeing-app
dotnet test --project digital-wellbeing-app/Tests/Tests.csproj
dotnet format
```

## Conventions
- PascalCase for public members, _camelCase for private fields
- Interfaces prefixed with I (IService, IRepository)
- Async/await for all I/O -- never block the UI thread
- MVVM: INotifyPropertyChanged for ViewModel properties
- Data binding for all UI state -- no direct UI manipulation from code-behind
- Background tasks via Task.Run, never on UI thread
- Dispose pattern for unmanaged resources (IDisposable)
- Nullable reference types enabled

## Prohibitions
- NEVER block the UI thread with synchronous I/O
- NEVER use Thread.Sleep in UI code -- use Task.Delay
- NEVER commit user secrets, connection strings, or .db files
- NEVER store secrets in workflow files
- NEVER run dotnet publish from agent context

## Workflow
- Plan before multi-file changes, execute after alignment
- After changes: spawn code-reviewer agent on the diff
- After new code: spawn test-writer if test framework exists
- Verify: run `dotnet build` and `dotnet test --project digital-wellbeing-app/Tests/Tests.csproj` before declaring done
- Suggest relevant available tools/skills when they fit the current task
