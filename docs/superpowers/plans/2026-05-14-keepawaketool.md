# KeepAwakeTool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a Windows v1 of KeepAwakeTool — a background utility that injects synthetic mouse/keyboard input to keep presence-aware apps marked Active, with optional display-off-after-injection (S1) and system-awake-only (S3) modes.

**Architecture:** Four-project .NET solution. `Core` holds platform-agnostic config, scheduling, and the activity engine behind interfaces. `Platform.Win` implements those interfaces via Win32 P/Invoke. `App` is an Avalonia tray-icon + settings-window host. `Core.Tests` drives the engine via xUnit + NSubstitute with fakes. macOS is deferred to v2 — same interfaces, different implementations.

**Tech Stack:** .NET 10, C#, Avalonia 12, Microsoft.Extensions.DependencyInjection, System.Text.Json, xUnit, NSubstitute. Single-file self-contained publish on `win-x64`.

---

## File Structure (locked decisions)

```
keepawaketool/
├── .editorconfig
├── .gitignore
├── global.json
├── Directory.Build.props
├── KeepAwakeTool.sln
├── src/
│   ├── KeepAwakeTool.Core/
│   │   ├── KeepAwakeTool.Core.csproj
│   │   ├── Config/
│   │   │   ├── AppConfig.cs                    # root record + nested records
│   │   │   ├── ConfigDefaults.cs               # AppConfig.Default
│   │   │   ├── ConfigClamping.cs               # range/enum sanitization
│   │   │   └── ConfigurationStore.cs           # load/save + FileSystemWatcher
│   │   ├── Activity/
│   │   │   ├── ActivityEngine.cs               # tick decision tree
│   │   │   ├── EngineState.cs                  # enum
│   │   │   ├── MouseMode.cs                    # enum
│   │   │   └── IClock.cs / SystemClock.cs      # testable time/delay
│   │   ├── Scheduling/
│   │   │   ├── WorkingHoursPolicy.cs           # pure function
│   │   │   └── Scheduler.cs                    # composes engine + schedule + pause flags
│   │   ├── Power/
│   │   │   └── PowerModeController.cs          # S1/S3 toggle wiring
│   │   ├── Hotkey/
│   │   │   └── Hotkey.cs                       # record + parser
│   │   ├── Platform/
│   │   │   ├── IInputSimulator.cs
│   │   │   ├── IPowerManager.cs
│   │   │   ├── IIdleMonitor.cs
│   │   │   ├── IAutoStartManager.cs
│   │   │   ├── IGlobalHotkeyService.cs
│   │   │   └── VirtualKey.cs                   # enum: F13/F14/F15
│   │   └── Diagnostics/
│   │       └── FileLogger.cs
│   ├── KeepAwakeTool.Platform.Win/
│   │   ├── KeepAwakeTool.Platform.Win.csproj
│   │   ├── Interop/
│   │   │   ├── User32.cs                       # P/Invoke
│   │   │   ├── Kernel32.cs
│   │   │   ├── InputStructs.cs                 # INPUT, MOUSEINPUT, KEYBDINPUT
│   │   │   └── ExecutionState.cs               # [Flags] enum
│   │   ├── WindowsInputSimulator.cs
│   │   ├── WindowsIdleMonitor.cs
│   │   ├── WindowsPowerManager.cs
│   │   ├── WindowsAutoStartManager.cs
│   │   ├── WindowsGlobalHotkeyService.cs
│   │   └── HiddenMessageWindow.cs              # Win32 message-only window for WM_HOTKEY
│   └── KeepAwakeTool.App/
│       ├── KeepAwakeTool.App.csproj
│       ├── Program.cs
│       ├── App.axaml + App.axaml.cs
│       ├── Composition/
│       │   └── ServiceRegistration.cs          # DI wiring
│       ├── SingleInstance/
│       │   └── SingleInstanceGuard.cs
│       ├── Tray/
│       │   ├── TrayIconController.cs
│       │   └── Assets/                         # png icons per state
│       ├── ViewModels/
│       │   ├── SettingsViewModel.cs
│       │   ├── GeneralTabViewModel.cs
│       │   ├── ActivityTabViewModel.cs
│       │   ├── PowerTabViewModel.cs
│       │   ├── ScheduleTabViewModel.cs
│       │   └── HotkeyTabViewModel.cs
│       └── Views/
│           ├── SettingsWindow.axaml + .cs
│           ├── GeneralTab.axaml + .cs
│           ├── ActivityTab.axaml + .cs
│           ├── PowerTab.axaml + .cs
│           ├── ScheduleTab.axaml + .cs
│           └── HotkeyTab.axaml + .cs
├── tests/
│   └── KeepAwakeTool.Core.Tests/
│       ├── KeepAwakeTool.Core.Tests.csproj
│       ├── Fakes/
│       │   ├── FakeClock.cs
│       │   ├── FakeInputSimulator.cs
│       │   ├── FakeIdleMonitor.cs
│       │   └── FakePowerManager.cs
│       ├── ConfigurationStoreTests.cs
│       ├── ConfigClampingTests.cs
│       ├── ActivityEngineTests.cs
│       ├── WorkingHoursPolicyTests.cs
│       ├── SchedulerTests.cs
│       └── PowerModeControllerTests.cs
├── docs/
│   ├── superpowers/specs/2026-05-14-keepawaketool-design.md   # (already committed)
│   ├── superpowers/plans/2026-05-14-keepawaketool.md          # (this file)
│   └── manual-smoke.md                                         # manual test checklist
└── .github/
    └── workflows/
        └── ci.yml
```

---

## Phase 1 — Repo scaffolding

### Task 1: Add baseline tooling files

**Files:**
- Create: `.gitignore`
- Create: `.editorconfig`
- Create: `global.json`
- Create: `Directory.Build.props`

- [ ] **Step 1: Create .gitignore**

Write `.gitignore`:

```
# .NET
bin/
obj/
*.user
*.suo
.vs/

# Publish output
publish/
out/

# Rider/VS
.idea/
*.DotSettings.user

# Logs
*.log
logs/

# OS
Thumbs.db
.DS_Store

# Claude per-project local
.claude/
```

- [ ] **Step 2: Create .editorconfig**

Write `.editorconfig`:

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{json,yml,yaml,xml,axaml,csproj,props,targets}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false
```

- [ ] **Step 3: Create global.json**

Write `global.json` pinning the .NET 10 SDK band:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 4: Create Directory.Build.props**

Write `Directory.Build.props` to share TFM and language settings:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors>NU1903</WarningsNotAsErrors>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Commit**

```bash
git add .gitignore .editorconfig global.json Directory.Build.props
git commit -m "chore: add baseline repo tooling (gitignore, editorconfig, global.json, Directory.Build.props)"
```

---

### Task 2: Create solution and four projects

**Files:**
- Create: `KeepAwakeTool.sln`
- Create: `src/KeepAwakeTool.Core/KeepAwakeTool.Core.csproj`
- Create: `src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj`
- Create: `src/KeepAwakeTool.App/KeepAwakeTool.App.csproj`
- Create: `tests/KeepAwakeTool.Core.Tests/KeepAwakeTool.Core.Tests.csproj`

- [ ] **Step 1: Create the solution**

```bash
dotnet new sln -n KeepAwakeTool
```

- [ ] **Step 2: Create Core class library**

```bash
dotnet new classlib -o src/KeepAwakeTool.Core --framework net10.0
rm src/KeepAwakeTool.Core/Class1.cs
```

Replace generated `src/KeepAwakeTool.Core/KeepAwakeTool.Core.csproj` contents with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>KeepAwakeTool.Core</RootNamespace>
    <AssemblyName>KeepAwakeTool.Core</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Platform.Win class library**

```bash
dotnet new classlib -o src/KeepAwakeTool.Platform.Win --framework net10.0-windows
rm src/KeepAwakeTool.Platform.Win/Class1.cs
```

Replace `src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <RootNamespace>KeepAwakeTool.Platform.Win</RootNamespace>
    <AssemblyName>KeepAwakeTool.Platform.Win</AssemblyName>
    <UseWindowsForms>false</UseWindowsForms>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\KeepAwakeTool.Core\KeepAwakeTool.Core.csproj" />
    <PackageReference Include="Microsoft.Win32.Registry" Version="5.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create App project (Avalonia template)**

```bash
dotnet new install Avalonia.Templates::12.0.0
dotnet new avalonia.app -o src/KeepAwakeTool.App --framework net10.0
```

Edit `src/KeepAwakeTool.App/KeepAwakeTool.App.csproj` to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>KeepAwakeTool.App</RootNamespace>
    <AssemblyName>KeepAwakeTool</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <UseAvalonia>true</UseAvalonia>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\KeepAwakeTool.Core\KeepAwakeTool.Core.csproj" />
    <ProjectReference Include="..\KeepAwakeTool.Platform.Win\KeepAwakeTool.Platform.Win.csproj" />
    <PackageReference Include="Avalonia" Version="12.0.0" />
    <PackageReference Include="Avalonia.Desktop" Version="12.0.0" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.0" />
    <PackageReference Include="Avalonia.Controls.ItemsRepeater" Version="12.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create Tests project**

```bash
dotnet new xunit -o tests/KeepAwakeTool.Core.Tests --framework net10.0
rm tests/KeepAwakeTool.Core.Tests/UnitTest1.cs
```

Replace `tests/KeepAwakeTool.Core.Tests/KeepAwakeTool.Core.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>KeepAwakeTool.Core.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\KeepAwakeTool.Core\KeepAwakeTool.Core.csproj" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Add projects to solution**

```bash
dotnet sln add src/KeepAwakeTool.Core/KeepAwakeTool.Core.csproj
dotnet sln add src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj
dotnet sln add src/KeepAwakeTool.App/KeepAwakeTool.App.csproj
dotnet sln add tests/KeepAwakeTool.Core.Tests/KeepAwakeTool.Core.Tests.csproj
```

- [ ] **Step 7: Verify build**

```bash
dotnet restore
dotnet build
```

Expected: build succeeds for all four projects.

- [ ] **Step 8: Commit**

```bash
git add KeepAwakeTool.sln src/ tests/
git commit -m "chore: scaffold solution with Core, Platform.Win, App, and Core.Tests projects"
```

---

## Phase 2 — Configuration model

### Task 3: Define AppConfig record graph + enums

**Files:**
- Create: `src/KeepAwakeTool.Core/Activity/MouseMode.cs`
- Create: `src/KeepAwakeTool.Core/Platform/VirtualKey.cs`
- Create: `src/KeepAwakeTool.Core/Config/AppConfig.cs`
- Create: `src/KeepAwakeTool.Core/Config/ConfigDefaults.cs`

- [ ] **Step 1: Create MouseMode enum**

Write `src/KeepAwakeTool.Core/Activity/MouseMode.cs`:

```csharp
namespace KeepAwakeTool.Core.Activity;

public enum MouseMode
{
    Invisible,
    Jiggle
}
```

- [ ] **Step 2: Create VirtualKey enum**

Write `src/KeepAwakeTool.Core/Platform/VirtualKey.cs`:

```csharp
namespace KeepAwakeTool.Core.Platform;

public enum VirtualKey
{
    F13,
    F14,
    F15
}
```

- [ ] **Step 3: Create config records**

Write `src/KeepAwakeTool.Core/Config/AppConfig.cs`:

```csharp
using System.Collections.Generic;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Config;

public sealed record AppConfig
{
    public int SchemaVersion { get; init; } = 1;
    public ActivityConfig Activity { get; init; } = new();
    public PowerConfig Power { get; init; } = new();
    public ScheduleConfig Schedule { get; init; } = new();
    public HotkeyConfig Hotkey { get; init; } = new();
    public StartupConfig Startup { get; init; } = new();
    public UiConfig Ui { get; init; } = new();
}

public sealed record ActivityConfig
{
    public int IntervalSeconds { get; init; } = 60;
    public int IdleThresholdSeconds { get; init; } = 30;
    public MouseConfig Mouse { get; init; } = new();
    public KeystrokeConfig Keystroke { get; init; } = new();
}

public sealed record MouseConfig
{
    public MouseMode Mode { get; init; } = MouseMode.Invisible;
    public int JigglePixels { get; init; } = 1;
}

public sealed record KeystrokeConfig
{
    public bool Enabled { get; init; } = true;
    public VirtualKey Key { get; init; } = VirtualKey.F15;
    public int EveryNthCycle { get; init; } = 3;
}

public sealed record PowerConfig
{
    public bool ForceDisplayOffAfterInjection { get; init; } = false;
    public bool PowerSaveMode { get; init; } = false;
}

public sealed record ScheduleConfig
{
    public bool Enabled { get; init; } = false;
    public string StartTime { get; init; } = "09:00";
    public string EndTime { get; init; } = "18:00";
    public IReadOnlyList<DayOfWeek> Days { get; init; } = new[]
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    };
}

public sealed record HotkeyConfig
{
    public bool Enabled { get; init; } = false;
    public string Combination { get; init; } = "Ctrl+Alt+P";
}

public sealed record StartupConfig
{
    public bool AutoStartOnLogin { get; init; } = false;
    public bool StartMinimizedToTray { get; init; } = true;
}

public sealed record UiConfig
{
    public string Theme { get; init; } = "System";
    public bool ShowHeartbeatAnimation { get; init; } = false;
}
```

- [ ] **Step 4: Create ConfigDefaults**

Write `src/KeepAwakeTool.Core/Config/ConfigDefaults.cs`:

```csharp
namespace KeepAwakeTool.Core.Config;

public static class ConfigDefaults
{
    public static AppConfig Default() => new();
}
```

- [ ] **Step 5: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Core/KeepAwakeTool.Core.csproj
git add src/KeepAwakeTool.Core/
git commit -m "feat(core): define AppConfig record graph and enums with v1 defaults"
```

---

### Task 4: Config clamping (TDD)

**Files:**
- Create: `src/KeepAwakeTool.Core/Config/ConfigClamping.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/ConfigClampingTests.cs`

- [ ] **Step 1: Write the failing tests**

Write `tests/KeepAwakeTool.Core.Tests/ConfigClampingTests.cs`:

```csharp
using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ConfigClampingTests
{
    [Fact]
    public void IntervalSeconds_below_minimum_is_clamped_to_10()
    {
        var input = new AppConfig { Activity = new ActivityConfig { IntervalSeconds = 1 } };
        ConfigClamping.Sanitize(input).Activity.IntervalSeconds.Should().Be(10);
    }

    [Fact]
    public void IntervalSeconds_above_maximum_is_clamped_to_240()
    {
        var input = new AppConfig { Activity = new ActivityConfig { IntervalSeconds = 10_000 } };
        ConfigClamping.Sanitize(input).Activity.IntervalSeconds.Should().Be(240);
    }

    [Fact]
    public void IdleThresholdSeconds_outside_range_is_clamped()
    {
        var low  = new AppConfig { Activity = new ActivityConfig { IdleThresholdSeconds = 0 } };
        var high = new AppConfig { Activity = new ActivityConfig { IdleThresholdSeconds = 99_999 } };
        ConfigClamping.Sanitize(low).Activity.IdleThresholdSeconds.Should().Be(5);
        ConfigClamping.Sanitize(high).Activity.IdleThresholdSeconds.Should().Be(120);
    }

    [Fact]
    public void JigglePixels_below_minimum_becomes_1()
    {
        var input = new AppConfig
        {
            Activity = new ActivityConfig { Mouse = new MouseConfig { JigglePixels = 0 } }
        };
        ConfigClamping.Sanitize(input).Activity.Mouse.JigglePixels.Should().Be(1);
    }

    [Fact]
    public void EveryNthCycle_below_minimum_becomes_1()
    {
        var input = new AppConfig
        {
            Activity = new ActivityConfig { Keystroke = new KeystrokeConfig { EveryNthCycle = 0 } }
        };
        ConfigClamping.Sanitize(input).Activity.Keystroke.EveryNthCycle.Should().Be(1);
    }

    [Fact]
    public void Valid_config_is_unchanged()
    {
        var input = ConfigDefaults.Default();
        ConfigClamping.Sanitize(input).Should().Be(input);
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ConfigClampingTests
```

Expected: build fails with "type ConfigClamping not found".

- [ ] **Step 3: Implement ConfigClamping**

Write `src/KeepAwakeTool.Core/Config/ConfigClamping.cs`:

```csharp
using System;

namespace KeepAwakeTool.Core.Config;

public static class ConfigClamping
{
    public static AppConfig Sanitize(AppConfig input) => input with
    {
        Activity = input.Activity with
        {
            IntervalSeconds      = Clamp(input.Activity.IntervalSeconds, 10, 240),
            IdleThresholdSeconds = Clamp(input.Activity.IdleThresholdSeconds, 5, 120),
            Mouse = input.Activity.Mouse with
            {
                JigglePixels = Clamp(input.Activity.Mouse.JigglePixels, 1, 10)
            },
            Keystroke = input.Activity.Keystroke with
            {
                EveryNthCycle = Clamp(input.Activity.Keystroke.EveryNthCycle, 1, 10)
            }
        }
    };

    private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ConfigClampingTests
```

Expected: 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Core/Config/ConfigClamping.cs tests/KeepAwakeTool.Core.Tests/ConfigClampingTests.cs
git commit -m "feat(core): clamp numeric config fields to safe ranges on load"
```

---

### Task 5: ConfigurationStore — load & defaults-on-missing (TDD)

**Files:**
- Create: `src/KeepAwakeTool.Core/Config/ConfigurationStore.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/ConfigurationStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

Write `tests/KeepAwakeTool.Core.Tests/ConfigurationStoreTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using KeepAwakeTool.Core.Config;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ConfigurationStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _path;

    public ConfigurationStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "kat-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _path = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_writes_defaults_when_file_missing()
    {
        var store = new ConfigurationStore(_path);
        var loaded = store.Load();
        loaded.Should().Be(ConfigDefaults.Default());
        File.Exists(_path).Should().BeTrue();
    }

    [Fact]
    public void Load_returns_persisted_values()
    {
        File.WriteAllText(_path, """
            {
              "schemaVersion": 1,
              "activity": { "intervalSeconds": 90 }
            }
            """);
        var loaded = new ConfigurationStore(_path).Load();
        loaded.Activity.IntervalSeconds.Should().Be(90);
    }

    [Fact]
    public void Load_clamps_out_of_range_values()
    {
        File.WriteAllText(_path, """
            { "schemaVersion": 1, "activity": { "intervalSeconds": 5 } }
            """);
        new ConfigurationStore(_path).Load().Activity.IntervalSeconds.Should().Be(10);
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ConfigurationStoreTests
```

Expected: build fails ("ConfigurationStore not found").

- [ ] **Step 3: Implement ConfigurationStore (load + atomic write)**

Write `src/KeepAwakeTool.Core/Config/ConfigurationStore.cs`:

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeepAwakeTool.Core.Config;

public sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _path;

    public ConfigurationStore(string path) => _path = path;

    public string Path => _path;

    public AppConfig Load()
    {
        if (!File.Exists(_path))
        {
            var defaults = ConfigDefaults.Default();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var parsed = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? ConfigDefaults.Default();
            return ConfigClamping.Sanitize(parsed);
        }
        catch (JsonException)
        {
            QuarantineCorrupt();
            var defaults = ConfigDefaults.Default();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
        if (File.Exists(_path)) File.Replace(tmp, _path, destinationBackupFileName: null);
        else File.Move(tmp, _path);
    }

    private void QuarantineCorrupt()
    {
        var backup = $"{_path}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        try { File.Move(_path, backup); } catch { /* best effort */ }
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ConfigurationStoreTests
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Core/Config/ConfigurationStore.cs tests/KeepAwakeTool.Core.Tests/ConfigurationStoreTests.cs
git commit -m "feat(core): ConfigurationStore loads defaults, persists, sanitizes input"
```

---

### Task 6: ConfigurationStore — corruption recovery + change events (TDD)

**Files:**
- Modify: `src/KeepAwakeTool.Core/Config/ConfigurationStore.cs`
- Modify: `tests/KeepAwakeTool.Core.Tests/ConfigurationStoreTests.cs`

- [ ] **Step 1: Add failing test for corruption quarantine**

Append to `ConfigurationStoreTests.cs`:

```csharp
[Fact]
public void Load_quarantines_corrupt_file_and_returns_defaults()
{
    File.WriteAllText(_path, "{ not valid json");
    var loaded = new ConfigurationStore(_path).Load();
    loaded.Should().Be(ConfigDefaults.Default());
    Directory.EnumerateFiles(_tempDir, "config.json.corrupt.*.json").Should().NotBeEmpty();
}

[Fact]
public void Save_then_Load_round_trips_values()
{
    var store = new ConfigurationStore(_path);
    var cfg = ConfigDefaults.Default() with
    {
        Power = new PowerConfig { ForceDisplayOffAfterInjection = true, PowerSaveMode = false }
    };
    store.Save(cfg);
    store.Load().Power.ForceDisplayOffAfterInjection.Should().BeTrue();
}
```

- [ ] **Step 2: Run tests — verify corruption test passes (Task 5 implementation handles it)**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ConfigurationStoreTests
```

Expected: all 5 tests pass.

- [ ] **Step 3: Add `Changed` event + `StartWatching()` to ConfigurationStore**

Replace `src/KeepAwakeTool.Core/Config/ConfigurationStore.cs` `Save` method's surroundings — add the watcher members. Insert these members inside the class (after the `_path` field):

```csharp
    private FileSystemWatcher? _watcher;
    public event EventHandler<AppConfig>? Changed;

    public void StartWatching()
    {
        if (_watcher is not null) return;
        var dir = System.IO.Path.GetDirectoryName(_path)!;
        var name = System.IO.Path.GetFileName(_path);
        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) =>
        {
            try { Changed?.Invoke(this, Load()); } catch { /* swallow; logged elsewhere */ }
        };
    }

    public void StopWatching()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
```

- [ ] **Step 4: Add failing test for change events**

Append to `ConfigurationStoreTests.cs`:

```csharp
[Fact]
public async Task Changed_event_fires_when_file_is_modified()
{
    var store = new ConfigurationStore(_path);
    _ = store.Load();
    store.StartWatching();

    var tcs = new TaskCompletionSource<AppConfig>();
    store.Changed += (_, cfg) => tcs.TrySetResult(cfg);

    var updated = ConfigDefaults.Default() with
    {
        Activity = ConfigDefaults.Default().Activity with { IntervalSeconds = 120 }
    };
    store.Save(updated);

    var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    received.Activity.IntervalSeconds.Should().Be(120);

    store.StopWatching();
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ConfigurationStoreTests
```

Expected: all 6 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/KeepAwakeTool.Core/Config/ConfigurationStore.cs tests/KeepAwakeTool.Core.Tests/ConfigurationStoreTests.cs
git commit -m "feat(core): ConfigurationStore quarantines corrupt files and emits Changed events"
```

---

## Phase 3 — Platform interfaces

### Task 7: Define platform-agnostic interfaces

**Files:**
- Create: `src/KeepAwakeTool.Core/Platform/IInputSimulator.cs`
- Create: `src/KeepAwakeTool.Core/Platform/IPowerManager.cs`
- Create: `src/KeepAwakeTool.Core/Platform/IIdleMonitor.cs`
- Create: `src/KeepAwakeTool.Core/Platform/IAutoStartManager.cs`
- Create: `src/KeepAwakeTool.Core/Platform/IGlobalHotkeyService.cs`
- Create: `src/KeepAwakeTool.Core/Hotkey/Hotkey.cs`
- Create: `src/KeepAwakeTool.Core/Activity/IClock.cs`
- Create: `src/KeepAwakeTool.Core/Activity/SystemClock.cs`
- Create: `src/KeepAwakeTool.Core/Activity/EngineState.cs`

- [ ] **Step 1: Create EngineState enum**

Write `src/KeepAwakeTool.Core/Activity/EngineState.cs`:

```csharp
namespace KeepAwakeTool.Core.Activity;

public enum EngineState { Stopped, Running, Paused, PowerSave }
```

- [ ] **Step 2: Create IClock + SystemClock**

Write `src/KeepAwakeTool.Core/Activity/IClock.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeepAwakeTool.Core.Activity;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset LocalNow { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
```

Write `src/KeepAwakeTool.Core/Activity/SystemClock.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeepAwakeTool.Core.Activity;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset LocalNow => DateTimeOffset.Now;
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}
```

- [ ] **Step 3: Create platform interfaces**

Write `src/KeepAwakeTool.Core/Platform/IInputSimulator.cs`:

```csharp
using KeepAwakeTool.Core.Activity;

namespace KeepAwakeTool.Core.Platform;

public interface IInputSimulator
{
    void MoveMouse(MouseMode mode, int jigglePixels);
    void SendKey(VirtualKey key);
}
```

Write `src/KeepAwakeTool.Core/Platform/IPowerManager.cs`:

```csharp
namespace KeepAwakeTool.Core.Platform;

public interface IPowerManager
{
    void KeepSystemAwake(bool on);
    void ForceDisplayOff();
}
```

Write `src/KeepAwakeTool.Core/Platform/IIdleMonitor.cs`:

```csharp
using System;

namespace KeepAwakeTool.Core.Platform;

public interface IIdleMonitor
{
    TimeSpan TimeSinceLastUserInput();
}
```

Write `src/KeepAwakeTool.Core/Platform/IAutoStartManager.cs`:

```csharp
namespace KeepAwakeTool.Core.Platform;

public interface IAutoStartManager
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
```

Write `src/KeepAwakeTool.Core/Platform/IGlobalHotkeyService.cs`:

```csharp
using System;
using KeepAwakeTool.Core.Hotkey;

namespace KeepAwakeTool.Core.Platform;

public interface IGlobalHotkeyService
{
    bool TryRegister(Hotkey.Hotkey hotkey, Action onPressed);
    void Unregister();
}
```

- [ ] **Step 4: Create Hotkey record**

Write `src/KeepAwakeTool.Core/Hotkey/Hotkey.cs`:

```csharp
using System;
using System.Linq;

namespace KeepAwakeTool.Core.Hotkey;

[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public static Hotkey Parse(string combination)
    {
        if (string.IsNullOrWhiteSpace(combination))
            throw new ArgumentException("Combination is empty", nameof(combination));

        var parts = combination.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var mods = HotkeyModifiers.None;
        string? key = null;

        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl": case "control":   mods |= HotkeyModifiers.Ctrl;  break;
                case "alt":                    mods |= HotkeyModifiers.Alt;   break;
                case "shift":                  mods |= HotkeyModifiers.Shift; break;
                case "win": case "windows":    mods |= HotkeyModifiers.Win;   break;
                default:
                    if (key is not null)
                        throw new ArgumentException($"Multiple non-modifier keys in '{combination}'");
                    key = p.ToUpperInvariant();
                    break;
            }
        }

        if (key is null) throw new ArgumentException($"No non-modifier key in '{combination}'");
        return new Hotkey(mods, key);
    }

    public override string ToString()
    {
        var parts = new[]
        {
            (Modifiers.HasFlag(HotkeyModifiers.Ctrl),  "Ctrl"),
            (Modifiers.HasFlag(HotkeyModifiers.Alt),   "Alt"),
            (Modifiers.HasFlag(HotkeyModifiers.Shift), "Shift"),
            (Modifiers.HasFlag(HotkeyModifiers.Win),   "Win"),
        }.Where(t => t.Item1).Select(t => t.Item2);
        return string.Join("+", parts.Append(Key));
    }
}
```

- [ ] **Step 5: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Core
git add src/KeepAwakeTool.Core/
git commit -m "feat(core): define platform interfaces, IClock, Hotkey record, EngineState"
```

---

## Phase 4 — ActivityEngine (TDD)

### Task 8: ActivityEngine — happy-path tick (TDD)

**Files:**
- Create: `tests/KeepAwakeTool.Core.Tests/Fakes/FakeClock.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/Fakes/FakeInputSimulator.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/Fakes/FakeIdleMonitor.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/Fakes/FakePowerManager.cs`
- Create: `src/KeepAwakeTool.Core/Activity/ActivityEngine.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs`

- [ ] **Step 1: Create fakes**

Write `tests/KeepAwakeTool.Core.Tests/Fakes/FakeClock.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Activity;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakeClock : IClock
{
    private DateTimeOffset _now = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    public TimeSpan LastDelay { get; private set; } = TimeSpan.Zero;
    public DateTimeOffset UtcNow => _now;
    public DateTimeOffset LocalNow => _now;
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        LastDelay = delay;
        _now = _now.Add(delay);
        return Task.CompletedTask;
    }
    public void Advance(TimeSpan by) => _now = _now.Add(by);
    public void SetLocal(DateTimeOffset value) => _now = value;
}
```

Write `tests/KeepAwakeTool.Core.Tests/Fakes/FakeInputSimulator.cs`:

```csharp
using System.Collections.Generic;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakeInputSimulator : IInputSimulator
{
    public List<(MouseMode mode, int px)> MouseMoves { get; } = new();
    public List<VirtualKey> Keys { get; } = new();
    public void MoveMouse(MouseMode mode, int jigglePixels) => MouseMoves.Add((mode, jigglePixels));
    public void SendKey(VirtualKey key) => Keys.Add(key);
}
```

Write `tests/KeepAwakeTool.Core.Tests/Fakes/FakeIdleMonitor.cs`:

```csharp
using System;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakeIdleMonitor : IIdleMonitor
{
    public TimeSpan Value { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan TimeSinceLastUserInput() => Value;
}
```

Write `tests/KeepAwakeTool.Core.Tests/Fakes/FakePowerManager.cs`:

```csharp
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakePowerManager : IPowerManager
{
    public int KeepAwakeOnCalls { get; private set; }
    public int KeepAwakeOffCalls { get; private set; }
    public int ForceDisplayOffCalls { get; private set; }
    public void KeepSystemAwake(bool on) { if (on) KeepAwakeOnCalls++; else KeepAwakeOffCalls++; }
    public void ForceDisplayOff() => ForceDisplayOffCalls++;
}
```

- [ ] **Step 2: Write the failing test**

Write `tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs`:

```csharp
using System.Threading;
using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Tests.Fakes;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ActivityEngineTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeInputSimulator _input = new();
    private readonly FakeIdleMonitor _idle = new() { Value = TimeSpan.FromMinutes(5) };
    private readonly FakePowerManager _power = new();

    private ActivityEngine BuildEngine(AppConfig? cfg = null)
        => new(_input, _idle, _power, _clock, cfg ?? ConfigDefaults.Default());

    [Fact]
    public async Task Tick_moves_mouse_when_user_is_idle()
    {
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().ContainSingle()
            .Which.mode.Should().Be(MouseMode.Invisible);
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ActivityEngineTests
```

Expected: build fails ("ActivityEngine not found").

- [ ] **Step 4: Implement minimal ActivityEngine**

Write `src/KeepAwakeTool.Core/Activity/ActivityEngine.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Activity;

public sealed class ActivityEngine
{
    private readonly IInputSimulator _input;
    private readonly IIdleMonitor _idle;
    private readonly IPowerManager _power;
    private readonly IClock _clock;
    private AppConfig _config;
    private long _tickCount;

    public ActivityEngine(IInputSimulator input, IIdleMonitor idle, IPowerManager power, IClock clock, AppConfig config)
    {
        _input = input; _idle = idle; _power = power; _clock = clock; _config = config;
    }

    public AppConfig Config => _config;
    public long TickCount => _tickCount;
    public bool HotkeyPaused { get; set; }
    public bool WithinWorkingHours { get; set; } = true;

    public void UpdateConfig(AppConfig newConfig) => _config = newConfig;

    public async Task TickAsync(CancellationToken ct)
    {
        _tickCount++;

        if (_config.Power.PowerSaveMode) return;
        if (!WithinWorkingHours) return;
        if (HotkeyPaused) return;
        if (_idle.TimeSinceLastUserInput() < TimeSpan.FromSeconds(_config.Activity.IdleThresholdSeconds)) return;

        _input.MoveMouse(_config.Activity.Mouse.Mode, _config.Activity.Mouse.JigglePixels);

        if (_config.Activity.Keystroke.Enabled && (_tickCount % _config.Activity.Keystroke.EveryNthCycle == 0))
            _input.SendKey(_config.Activity.Keystroke.Key);

        if (_config.Power.ForceDisplayOffAfterInjection)
        {
            await _clock.DelayAsync(TimeSpan.FromMilliseconds(200), ct);
            _power.ForceDisplayOff();
        }
    }
}
```

- [ ] **Step 5: Run tests — expect pass**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ActivityEngineTests
```

Expected: 1 test passes.

- [ ] **Step 6: Commit**

```bash
git add src/KeepAwakeTool.Core/Activity/ActivityEngine.cs tests/KeepAwakeTool.Core.Tests/
git commit -m "feat(core): ActivityEngine emits mouse move on each tick when user is idle"
```

---

### Task 9: ActivityEngine — gates (smart-pause, S3, hotkey, schedule) (TDD)

**Files:**
- Modify: `tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `ActivityEngineTests.cs`:

```csharp
[Fact]
public async Task Tick_skips_when_user_recently_active()
{
    _idle.Value = TimeSpan.FromSeconds(5);   // less than default 30s threshold
    var engine = BuildEngine();
    await engine.TickAsync(CancellationToken.None);
    _input.MouseMoves.Should().BeEmpty();
}

[Fact]
public async Task Tick_skips_when_PowerSaveMode_on()
{
    var cfg = ConfigDefaults.Default() with { Power = new PowerConfig { PowerSaveMode = true } };
    var engine = BuildEngine(cfg);
    await engine.TickAsync(CancellationToken.None);
    _input.MouseMoves.Should().BeEmpty();
    _power.ForceDisplayOffCalls.Should().Be(0);
}

[Fact]
public async Task Tick_skips_when_hotkey_paused()
{
    var engine = BuildEngine();
    engine.HotkeyPaused = true;
    await engine.TickAsync(CancellationToken.None);
    _input.MouseMoves.Should().BeEmpty();
}

[Fact]
public async Task Tick_skips_when_outside_working_hours()
{
    var engine = BuildEngine();
    engine.WithinWorkingHours = false;
    await engine.TickAsync(CancellationToken.None);
    _input.MouseMoves.Should().BeEmpty();
}
```

- [ ] **Step 2: Run tests — expect all pass (Task 8 already implemented gates)**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ActivityEngineTests
```

Expected: 5 tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs
git commit -m "test(core): cover ActivityEngine tick gates — idle threshold, S3, hotkey, schedule"
```

---

### Task 10: ActivityEngine — keystroke cadence + S1 display-off ordering (TDD)

**Files:**
- Modify: `tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to `ActivityEngineTests.cs`:

```csharp
[Fact]
public async Task Keystroke_fires_only_on_every_Nth_tick()
{
    var cfg = ConfigDefaults.Default() with
    {
        Activity = ConfigDefaults.Default().Activity with
        {
            Keystroke = new KeystrokeConfig { Enabled = true, Key = VirtualKey.F15, EveryNthCycle = 3 }
        }
    };
    var engine = BuildEngine(cfg);
    for (int i = 0; i < 6; i++) await engine.TickAsync(CancellationToken.None);

    _input.Keys.Should().HaveCount(2);
    _input.Keys.Should().AllSatisfy(k => k.Should().Be(VirtualKey.F15));
}

[Fact]
public async Task Keystroke_skipped_when_disabled()
{
    var cfg = ConfigDefaults.Default() with
    {
        Activity = ConfigDefaults.Default().Activity with
        {
            Keystroke = new KeystrokeConfig { Enabled = false, Key = VirtualKey.F15, EveryNthCycle = 1 }
        }
    };
    var engine = BuildEngine(cfg);
    for (int i = 0; i < 3; i++) await engine.TickAsync(CancellationToken.None);
    _input.Keys.Should().BeEmpty();
}

[Fact]
public async Task S1_ForceDisplayOff_runs_after_200ms_delay()
{
    var cfg = ConfigDefaults.Default() with
    {
        Power = new PowerConfig { ForceDisplayOffAfterInjection = true }
    };
    var engine = BuildEngine(cfg);
    await engine.TickAsync(CancellationToken.None);

    _input.MouseMoves.Should().ContainSingle();
    _clock.LastDelay.Should().Be(TimeSpan.FromMilliseconds(200));
    _power.ForceDisplayOffCalls.Should().Be(1);
}

[Fact]
public async Task S1_ForceDisplayOff_skipped_when_disabled()
{
    var engine = BuildEngine();
    await engine.TickAsync(CancellationToken.None);
    _power.ForceDisplayOffCalls.Should().Be(0);
}
```

- [ ] **Step 2: Run tests — expect pass**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter ActivityEngineTests
```

Expected: 9 tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs
git commit -m "test(core): verify keystroke cadence and S1 display-off delay ordering"
```

---

## Phase 5 — Scheduling

### Task 11: WorkingHoursPolicy (TDD)

**Files:**
- Create: `src/KeepAwakeTool.Core/Scheduling/WorkingHoursPolicy.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/WorkingHoursPolicyTests.cs`

- [ ] **Step 1: Write the failing tests**

Write `tests/KeepAwakeTool.Core.Tests/WorkingHoursPolicyTests.cs`:

```csharp
using System;
using FluentAssertions;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Scheduling;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class WorkingHoursPolicyTests
{
    private static DateTimeOffset MondayAt(int h, int m) =>
        new(2026, 5, 11, h, m, 0, TimeSpan.Zero);  // 2026-05-11 is a Monday

    private static DateTimeOffset SaturdayAt(int h, int m) =>
        new(2026, 5, 9, h, m, 0, TimeSpan.Zero);

    [Fact]
    public void Disabled_schedule_is_always_within_window()
    {
        var s = new ScheduleConfig { Enabled = false };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(3, 0)).Should().BeTrue();
    }

    [Fact]
    public void Inside_time_window_on_allowed_day_returns_true()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "09:00", EndTime = "18:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(12, 0)).Should().BeTrue();
    }

    [Fact]
    public void Outside_time_window_returns_false()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "09:00", EndTime = "18:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(20, 0)).Should().BeFalse();
    }

    [Fact]
    public void Day_not_in_allowed_list_returns_false()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "09:00", EndTime = "18:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, SaturdayAt(12, 0)).Should().BeFalse();
    }

    [Fact]
    public void Start_equal_or_after_end_means_all_day_treated_as_always_in()
    {
        var s = new ScheduleConfig
        {
            Enabled = true, StartTime = "18:00", EndTime = "09:00",
            Days = new[] { DayOfWeek.Monday }
        };
        WorkingHoursPolicy.IsWithinWindow(s, MondayAt(3, 0)).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter WorkingHoursPolicyTests
```

Expected: build fails ("WorkingHoursPolicy not found").

- [ ] **Step 3: Implement WorkingHoursPolicy**

Write `src/KeepAwakeTool.Core/Scheduling/WorkingHoursPolicy.cs`:

```csharp
using System;
using System.Globalization;
using System.Linq;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.Core.Scheduling;

public static class WorkingHoursPolicy
{
    public static bool IsWithinWindow(ScheduleConfig schedule, DateTimeOffset now)
    {
        if (!schedule.Enabled) return true;
        if (!schedule.Days.Contains(now.DayOfWeek)) return false;

        if (!TimeSpan.TryParseExact(schedule.StartTime, "hh\\:mm", CultureInfo.InvariantCulture, out var start)) return true;
        if (!TimeSpan.TryParseExact(schedule.EndTime,   "hh\\:mm", CultureInfo.InvariantCulture, out var end))   return true;
        if (start >= end) return true; // misconfig: treat as all-day

        var current = now.TimeOfDay;
        return current >= start && current < end;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter WorkingHoursPolicyTests
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Core/Scheduling/WorkingHoursPolicy.cs tests/KeepAwakeTool.Core.Tests/WorkingHoursPolicyTests.cs
git commit -m "feat(core): WorkingHoursPolicy decides whether current time falls in schedule"
```

---

### Task 12: Scheduler — composition + state transitions (TDD)

**Files:**
- Create: `src/KeepAwakeTool.Core/Scheduling/Scheduler.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/SchedulerTests.cs`

- [ ] **Step 1: Write the failing tests**

Write `tests/KeepAwakeTool.Core.Tests/SchedulerTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Scheduling;
using KeepAwakeTool.Core.Tests.Fakes;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class SchedulerTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeInputSimulator _input = new();
    private readonly FakeIdleMonitor _idle = new() { Value = TimeSpan.FromMinutes(5) };
    private readonly FakePowerManager _power = new();

    [Fact]
    public void State_starts_as_Stopped()
    {
        var engine = new ActivityEngine(_input, _idle, _power, _clock, ConfigDefaults.Default());
        var sched = new Scheduler(engine, () => ConfigDefaults.Default(), _clock);
        sched.State.Should().Be(EngineState.Stopped);
    }

    [Fact]
    public void Start_transitions_to_Running()
    {
        var engine = new ActivityEngine(_input, _idle, _power, _clock, ConfigDefaults.Default());
        var sched = new Scheduler(engine, () => ConfigDefaults.Default(), _clock);
        sched.Start();
        sched.State.Should().Be(EngineState.Running);
    }

    [Fact]
    public void TogglePause_round_trips_Running_and_Paused()
    {
        var engine = new ActivityEngine(_input, _idle, _power, _clock, ConfigDefaults.Default());
        var sched = new Scheduler(engine, () => ConfigDefaults.Default(), _clock);
        sched.Start();
        sched.TogglePause();
        sched.State.Should().Be(EngineState.Paused);
        sched.TogglePause();
        sched.State.Should().Be(EngineState.Running);
    }

    [Fact]
    public async Task RunOneTick_respects_schedule_window()
    {
        // Saturday at 03:00 — outside Mon-Fri 09-18
        _clock.SetLocal(new DateTimeOffset(2026, 5, 9, 3, 0, 0, TimeSpan.Zero));

        var cfg = ConfigDefaults.Default() with
        {
            Schedule = new ScheduleConfig
            {
                Enabled = true, StartTime = "09:00", EndTime = "18:00",
                Days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            }
        };
        var engine = new ActivityEngine(_input, _idle, _power, _clock, cfg);
        var sched = new Scheduler(engine, () => cfg, _clock);
        sched.Start();
        await sched.RunOneTickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOneTick_respects_PowerSave_mode()
    {
        var cfg = ConfigDefaults.Default() with { Power = new PowerConfig { PowerSaveMode = true } };
        var engine = new ActivityEngine(_input, _idle, _power, _clock, cfg);
        var sched = new Scheduler(engine, () => cfg, _clock);
        sched.Start();
        sched.ApplyConfig(cfg);
        await sched.RunOneTickAsync(CancellationToken.None);
        sched.State.Should().Be(EngineState.PowerSave);
        _input.MouseMoves.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter SchedulerTests
```

Expected: build fails ("Scheduler not found").

- [ ] **Step 3: Implement Scheduler**

Write `src/KeepAwakeTool.Core/Scheduling/Scheduler.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.Core.Scheduling;

public sealed class Scheduler
{
    private readonly ActivityEngine _engine;
    private readonly Func<AppConfig> _configProvider;
    private readonly IClock _clock;

    public Scheduler(ActivityEngine engine, Func<AppConfig> configProvider, IClock clock)
    {
        _engine = engine; _configProvider = configProvider; _clock = clock;
    }

    public EngineState State { get; private set; } = EngineState.Stopped;
    public event EventHandler<EngineState>? StateChanged;

    public void Start()  => SetState(EvaluateState());
    public void Stop()   => SetState(EngineState.Stopped);
    public void TogglePause()
    {
        if (State == EngineState.Running) { _engine.HotkeyPaused = true;  SetState(EngineState.Paused); }
        else if (State == EngineState.Paused) { _engine.HotkeyPaused = false; SetState(EngineState.Running); }
    }

    public void ApplyConfig(AppConfig cfg)
    {
        _engine.UpdateConfig(cfg);
        if (State != EngineState.Stopped) SetState(EvaluateState());
    }

    public async Task RunOneTickAsync(CancellationToken ct)
    {
        var cfg = _configProvider();
        _engine.UpdateConfig(cfg);
        _engine.WithinWorkingHours = WorkingHoursPolicy.IsWithinWindow(cfg.Schedule, _clock.LocalNow);
        SetState(EvaluateState());
        if (State == EngineState.Running) await _engine.TickAsync(ct);
    }

    private EngineState EvaluateState()
    {
        var cfg = _configProvider();
        if (cfg.Power.PowerSaveMode) return EngineState.PowerSave;
        if (_engine.HotkeyPaused) return EngineState.Paused;
        return EngineState.Running;
    }

    private void SetState(EngineState next)
    {
        if (State == next) return;
        State = next;
        StateChanged?.Invoke(this, next);
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter SchedulerTests
```

Expected: 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Core/Scheduling/Scheduler.cs tests/KeepAwakeTool.Core.Tests/SchedulerTests.cs
git commit -m "feat(core): Scheduler composes engine + schedule + power-save state machine"
```

---

### Task 13: PowerModeController (TDD)

**Files:**
- Create: `src/KeepAwakeTool.Core/Power/PowerModeController.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/PowerModeControllerTests.cs`

- [ ] **Step 1: Write the failing tests**

Write `tests/KeepAwakeTool.Core.Tests/PowerModeControllerTests.cs`:

```csharp
using FluentAssertions;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Power;
using KeepAwakeTool.Core.Tests.Fakes;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class PowerModeControllerTests
{
    [Fact]
    public void Start_calls_KeepSystemAwake_true()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Start(ConfigDefaults.Default().Power);
        pm.KeepAwakeOnCalls.Should().Be(1);
    }

    [Fact]
    public void Stop_calls_KeepSystemAwake_false()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Start(ConfigDefaults.Default().Power);
        ctrl.Stop();
        pm.KeepAwakeOffCalls.Should().Be(1);
    }

    [Fact]
    public void ApplyConfig_idempotent_for_same_state()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Start(new PowerConfig { ForceDisplayOffAfterInjection = false, PowerSaveMode = false });
        ctrl.ApplyConfig(new PowerConfig { ForceDisplayOffAfterInjection = false, PowerSaveMode = false });
        pm.KeepAwakeOnCalls.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run tests — expect compile error**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter PowerModeControllerTests
```

Expected: build fails ("PowerModeController not found").

- [ ] **Step 3: Implement PowerModeController**

Write `src/KeepAwakeTool.Core/Power/PowerModeController.cs`:

```csharp
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Power;

public sealed class PowerModeController
{
    private readonly IPowerManager _power;
    private bool _awake;

    public PowerModeController(IPowerManager power) => _power = power;

    public void Start(PowerConfig _)
    {
        if (_awake) return;
        _power.KeepSystemAwake(true);
        _awake = true;
    }

    public void ApplyConfig(PowerConfig _) { /* no-op for now; reserved for future per-config logic */ }

    public void Stop()
    {
        if (!_awake) return;
        _power.KeepSystemAwake(false);
        _awake = false;
    }
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/KeepAwakeTool.Core.Tests --filter PowerModeControllerTests
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Core/Power/PowerModeController.cs tests/KeepAwakeTool.Core.Tests/PowerModeControllerTests.cs
git commit -m "feat(core): PowerModeController toggles KeepSystemAwake on start/stop"
```

---

## Phase 6 — Windows platform implementations

### Task 14: Win32 P/Invoke interop types

**Files:**
- Create: `src/KeepAwakeTool.Platform.Win/Interop/InputStructs.cs`
- Create: `src/KeepAwakeTool.Platform.Win/Interop/User32.cs`
- Create: `src/KeepAwakeTool.Platform.Win/Interop/Kernel32.cs`
- Create: `src/KeepAwakeTool.Platform.Win/Interop/ExecutionState.cs`

- [ ] **Step 1: Create ExecutionState flags**

Write `src/KeepAwakeTool.Platform.Win/Interop/ExecutionState.cs`:

```csharp
using System;

namespace KeepAwakeTool.Platform.Win.Interop;

[Flags]
internal enum ExecutionState : uint
{
    Continuous     = 0x80000000,
    SystemRequired = 0x00000001,
    DisplayRequired= 0x00000002,
    AwayModeRequired= 0x00000040
}
```

- [ ] **Step 2: Create INPUT structs**

Write `src/KeepAwakeTool.Platform.Win/Interop/InputStructs.cs`:

```csharp
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Win.Interop;

internal enum InputType : uint { Mouse = 0, Keyboard = 1, Hardware = 2 }

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public InputUnion U;
}

internal static class MouseFlags
{
    public const uint Move    = 0x0001;
    public const uint Absolute= 0x8000;
}

internal static class KeyFlags
{
    public const uint KeyDown = 0x0000;
    public const uint KeyUp   = 0x0002;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}
```

- [ ] **Step 3: Create User32 P/Invoke**

Write `src/KeepAwakeTool.Platform.Win/Interop/User32.cs`:

```csharp
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Win.Interop;

internal static class User32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint SendMessageW(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessageW(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    public const uint WM_SYSCOMMAND = 0x0112;
    public const int  SC_MONITORPOWER = 0xF170;
    public static readonly nint HWND_BROADCAST = new(0xFFFF);

    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT   = 0x0004;
    public const uint MOD_WIN     = 0x0008;
    public const uint WM_HOTKEY   = 0x0312;
}
```

- [ ] **Step 4: Create Kernel32 P/Invoke**

Write `src/KeepAwakeTool.Platform.Win/Interop/Kernel32.cs`:

```csharp
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Win.Interop;

internal static class Kernel32
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();
}
```

- [ ] **Step 5: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Platform.Win
git add src/KeepAwakeTool.Platform.Win/Interop/
git commit -m "feat(platform.win): add Win32 P/Invoke declarations for input, idle, power"
```

---

### Task 15: WindowsInputSimulator

**Files:**
- Create: `src/KeepAwakeTool.Platform.Win/WindowsInputSimulator.cs`

- [ ] **Step 1: Implement WindowsInputSimulator**

Write `src/KeepAwakeTool.Platform.Win/WindowsInputSimulator.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsInputSimulator : IInputSimulator
{
    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    public void MoveMouse(MouseMode mode, int jigglePixels)
    {
        if (mode == MouseMode.Invisible)
        {
            SendMouseMove(0, 0);
            return;
        }

        SendMouseMove( jigglePixels, 0);
        SendMouseMove(-jigglePixels, 0);
    }

    public void SendKey(VirtualKey key)
    {
        ushort vk = key switch
        {
            VirtualKey.F13 => 0x7C,
            VirtualKey.F14 => 0x7D,
            VirtualKey.F15 => 0x7E,
            _ => 0x7E
        };

        var down = new INPUT
        {
            type = (uint)InputType.Keyboard,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KeyFlags.KeyDown } }
        };
        var up = new INPUT
        {
            type = (uint)InputType.Keyboard,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KeyFlags.KeyUp } }
        };
        User32.SendInput(2, new[] { down, up }, InputSize);
    }

    private static void SendMouseMove(int dx, int dy)
    {
        var inp = new INPUT
        {
            type = (uint)InputType.Mouse,
            U = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MouseFlags.Move } }
        };
        User32.SendInput(1, new[] { inp }, InputSize);
    }
}
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Platform.Win
git add src/KeepAwakeTool.Platform.Win/WindowsInputSimulator.cs
git commit -m "feat(platform.win): WindowsInputSimulator emits SendInput for mouse and F-keys"
```

---

### Task 16: WindowsIdleMonitor

**Files:**
- Create: `src/KeepAwakeTool.Platform.Win/WindowsIdleMonitor.cs`

- [ ] **Step 1: Implement WindowsIdleMonitor**

Write `src/KeepAwakeTool.Platform.Win/WindowsIdleMonitor.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsIdleMonitor : IIdleMonitor
{
    public TimeSpan TimeSinceLastUserInput()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!User32.GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var tick = Kernel32.GetTickCount();
        var elapsedMs = unchecked(tick - info.dwTime);
        return TimeSpan.FromMilliseconds(elapsedMs);
    }
}
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Platform.Win
git add src/KeepAwakeTool.Platform.Win/WindowsIdleMonitor.cs
git commit -m "feat(platform.win): WindowsIdleMonitor reads GetLastInputInfo"
```

---

### Task 17: WindowsPowerManager

**Files:**
- Create: `src/KeepAwakeTool.Platform.Win/WindowsPowerManager.cs`

- [ ] **Step 1: Implement WindowsPowerManager**

Write `src/KeepAwakeTool.Platform.Win/WindowsPowerManager.cs`:

```csharp
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsPowerManager : IPowerManager
{
    public void KeepSystemAwake(bool on)
    {
        var flags = on
            ? ExecutionState.Continuous | ExecutionState.SystemRequired
            : ExecutionState.Continuous;
        Kernel32.SetThreadExecutionState(flags);
    }

    public void ForceDisplayOff()
    {
        // SC_MONITORPOWER lParam = 2 (off). PostMessage so we don't block.
        User32.PostMessageW(User32.HWND_BROADCAST, User32.WM_SYSCOMMAND, (nint)User32.SC_MONITORPOWER, 2);
    }
}
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Platform.Win
git add src/KeepAwakeTool.Platform.Win/WindowsPowerManager.cs
git commit -m "feat(platform.win): WindowsPowerManager toggles SystemRequired and forces monitor off"
```

---

### Task 18: WindowsAutoStartManager

**Files:**
- Create: `src/KeepAwakeTool.Platform.Win/WindowsAutoStartManager.cs`

- [ ] **Step 1: Implement WindowsAutoStartManager**

Write `src/KeepAwakeTool.Platform.Win/WindowsAutoStartManager.cs`:

```csharp
using System;
using Microsoft.Win32;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsAutoStartManager : IAutoStartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeepAwakeTool";

    private readonly string _exePath;
    public WindowsAutoStartManager(string exePath) => _exePath = exePath;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) as string is { } s && string.Equals(s.Trim('"'), _exePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Cannot open Run key");
        key.SetValue(ValueName, $"\"{_exePath}\"", RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Platform.Win
git add src/KeepAwakeTool.Platform.Win/WindowsAutoStartManager.cs
git commit -m "feat(platform.win): WindowsAutoStartManager maintains Run-key entry for autostart"
```

---

### Task 19: WindowsGlobalHotkeyService + HiddenMessageWindow

**Files:**
- Create: `src/KeepAwakeTool.Platform.Win/HiddenMessageWindow.cs`
- Create: `src/KeepAwakeTool.Platform.Win/WindowsGlobalHotkeyService.cs`

- [ ] **Step 1: Implement HiddenMessageWindow**

Write `src/KeepAwakeTool.Platform.Win/HiddenMessageWindow.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

internal sealed class HiddenMessageWindow : IDisposable
{
    private const string ClassName = "KeepAwakeTool.HiddenMessageWindow";
    private readonly WndProcDelegate _wndProc;
    private readonly Thread _pumpThread;
    private IntPtr _hwnd;
    private bool _disposed;

    public event Action<int>? HotkeyPressed;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS { public uint style; public WndProcDelegate lpfnWndProc; public int cbClsExtra; public int cbWndExtra; public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground; [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName; [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName; }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowExW(uint dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName, [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName, uint dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }

    private const uint WM_QUIT = 0x0012;
    private const uint HWND_MESSAGE = unchecked((uint)-3);
    private uint _pumpThreadId;
    private readonly ManualResetEventSlim _ready = new(false);

    public HiddenMessageWindow()
    {
        _wndProc = WndProc;
        _pumpThread = new Thread(Pump) { IsBackground = true, Name = "KAT-HotkeyPump" };
        _pumpThread.Start();
        _ready.Wait();
    }

    public IntPtr Handle => _hwnd;

    private void Pump()
    {
        _pumpThreadId = GetCurrentThreadId();
        var hInstance = GetModuleHandleW(null);
        var wc = new WNDCLASS { lpfnWndProc = _wndProc, hInstance = hInstance, lpszClassName = ClassName };
        RegisterClassW(ref wc);
        _hwnd = CreateWindowExW(0, ClassName, "", 0, 0, 0, 0, 0, (IntPtr)HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);
        _ready.Set();

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == User32.WM_HOTKEY) { HotkeyPressed?.Invoke((int)wParam); return IntPtr.Zero; }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PostThreadMessageW(_pumpThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _pumpThread.Join(1000);
        if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd);
    }
}
```

- [ ] **Step 2: Implement WindowsGlobalHotkeyService**

Write `src/KeepAwakeTool.Platform.Win/WindowsGlobalHotkeyService.cs`:

```csharp
using System;
using KeepAwakeTool.Core.Hotkey;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const int HotkeyId = 1;
    private HiddenMessageWindow? _window;
    private Action? _callback;

    public bool TryRegister(Hotkey hotkey, Action onPressed)
    {
        Unregister();
        _window = new HiddenMessageWindow();
        _window.HotkeyPressed += id => { if (id == HotkeyId) _callback?.Invoke(); };
        _callback = onPressed;

        uint mods =
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl)  ? User32.MOD_CONTROL : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)   ? User32.MOD_ALT     : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift) ? User32.MOD_SHIFT   : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)   ? User32.MOD_WIN     : 0u);

        var vk = MapKey(hotkey.Key);
        if (vk == 0) { Dispose(); return false; }

        var ok = User32.RegisterHotKey(_window.Handle, HotkeyId, mods, vk);
        if (!ok) { Dispose(); return false; }
        return true;
    }

    public void Unregister()
    {
        if (_window is null) return;
        User32.UnregisterHotKey(_window.Handle, HotkeyId);
        _window.Dispose();
        _window = null;
        _callback = null;
    }

    public void Dispose() => Unregister();

    private static uint MapKey(string key) => key.ToUpperInvariant() switch
    {
        var k when k.Length == 1 && k[0] >= 'A' && k[0] <= 'Z' => k[0],
        var k when k.Length == 1 && k[0] >= '0' && k[0] <= '9' => k[0],
        "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
        "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
        "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
        "P" => 0x50,
        _ => 0u
    };
}
```

- [ ] **Step 3: Build & commit**

```bash
dotnet build src/KeepAwakeTool.Platform.Win
git add src/KeepAwakeTool.Platform.Win/HiddenMessageWindow.cs src/KeepAwakeTool.Platform.Win/WindowsGlobalHotkeyService.cs
git commit -m "feat(platform.win): WindowsGlobalHotkeyService + HiddenMessageWindow for WM_HOTKEY"
```

---

## Phase 7 — App / Avalonia integration

### Task 20: App scaffold — Program.cs, DI wiring, App.axaml

**Files:**
- Create: `src/KeepAwakeTool.App/Program.cs`
- Create: `src/KeepAwakeTool.App/Composition/ServiceRegistration.cs`
- Modify: `src/KeepAwakeTool.App/App.axaml`
- Modify: `src/KeepAwakeTool.App/App.axaml.cs`
- Create: `src/KeepAwakeTool.App/app.manifest`

- [ ] **Step 1: Create app.manifest (DPI-aware)**

Write `src/KeepAwakeTool.App/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 2: Write ServiceRegistration**

Write `src/KeepAwakeTool.App/Composition/ServiceRegistration.cs`:

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Core.Power;
using KeepAwakeTool.Core.Scheduling;
using KeepAwakeTool.Platform.Win;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App.Composition;

public static class ServiceRegistration
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeepAwakeTool", "config.json");

        var store = new ConfigurationStore(configPath);
        var initial = store.Load();
        store.StartWatching();
        services.AddSingleton(store);

        AppConfig configSnapshot = initial;
        store.Changed += (_, cfg) => configSnapshot = cfg;
        services.AddSingleton<Func<AppConfig>>(_ => () => configSnapshot);

        services.AddSingleton<IClock, SystemClock>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("v1 supports Windows only.");

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
        services.AddSingleton<IInputSimulator, WindowsInputSimulator>();
        services.AddSingleton<IIdleMonitor, WindowsIdleMonitor>();
        services.AddSingleton<IPowerManager, WindowsPowerManager>();
        services.AddSingleton<IAutoStartManager>(_ => new WindowsAutoStartManager(exePath));
        services.AddSingleton<IGlobalHotkeyService, WindowsGlobalHotkeyService>();

        services.AddSingleton<ActivityEngine>(sp => new ActivityEngine(
            sp.GetRequiredService<IInputSimulator>(),
            sp.GetRequiredService<IIdleMonitor>(),
            sp.GetRequiredService<IPowerManager>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Func<AppConfig>>()()));

        services.AddSingleton<Scheduler>(sp => new Scheduler(
            sp.GetRequiredService<ActivityEngine>(),
            sp.GetRequiredService<Func<AppConfig>>(),
            sp.GetRequiredService<IClock>()));

        services.AddSingleton<PowerModeController>();

        return services.BuildServiceProvider();
    }
}
```

- [ ] **Step 3: Update App.axaml.cs**

Replace `src/KeepAwakeTool.App/App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KeepAwakeTool.App.Composition;
using KeepAwakeTool.App.Tray;
using System;

namespace KeepAwakeTool.App;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;
    public TrayIconController? Tray { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServiceRegistration.Build();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Tray = new TrayIconController(Services);
            Tray.Initialize();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 4: Write Program.cs**

Write `src/KeepAwakeTool.App/Program.cs`:

```csharp
using Avalonia;
using KeepAwakeTool.App.SingleInstance;
using System;

namespace KeepAwakeTool.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!SingleInstanceGuard.TryAcquire())
        {
            SingleInstanceGuard.SignalExistingInstance();
            return 0;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        finally
        {
            SingleInstanceGuard.Release();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
```

- [ ] **Step 5: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/
git commit -m "feat(app): wire DI, ConfigurationStore, and Avalonia App startup"
```

---

### Task 21: SingleInstanceGuard

**Files:**
- Create: `src/KeepAwakeTool.App/SingleInstance/SingleInstanceGuard.cs`

- [ ] **Step 1: Implement SingleInstanceGuard**

Write `src/KeepAwakeTool.App/SingleInstance/SingleInstanceGuard.cs`:

```csharp
using System.Threading;

namespace KeepAwakeTool.App.SingleInstance;

internal static class SingleInstanceGuard
{
    private const string MutexName = @"Global\KeepAwakeTool";
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
        return createdNew;
    }

    public static void SignalExistingInstance()
    {
        // v1: rely on tray icon being present; named-pipe focus message can be added later.
    }

    public static void Release()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* may have been abandoned */ }
        _mutex?.Dispose();
        _mutex = null;
    }
}
```

- [ ] **Step 2: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/SingleInstance/
git commit -m "feat(app): SingleInstanceGuard prevents duplicate launches via named mutex"
```

---

### Task 22: TrayIconController — icon + menu + state colors

**Files:**
- Create: `src/KeepAwakeTool.App/Tray/Assets/icon-stopped.ico`
- Create: `src/KeepAwakeTool.App/Tray/Assets/icon-running.ico`
- Create: `src/KeepAwakeTool.App/Tray/Assets/icon-paused.ico`
- Create: `src/KeepAwakeTool.App/Tray/Assets/icon-powersave.ico`
- Create: `src/KeepAwakeTool.App/Tray/TrayIconController.cs`

- [ ] **Step 1: Add icon assets (placeholders are acceptable for v1)**

Generate four 16×16 `.ico` files in `src/KeepAwakeTool.App/Tray/Assets/` filled with solid colors: gray, green, yellow, red. Any icon editor (or `magick convert -size 16x16 xc:green icon-running.ico`) works.

Add an entry to the csproj for the resources:

In `src/KeepAwakeTool.App/KeepAwakeTool.App.csproj`, add inside the existing `<ItemGroup>`:

```xml
    <AvaloniaResource Include="Tray/Assets/*.ico" />
```

- [ ] **Step 2: Implement TrayIconController**

Write `src/KeepAwakeTool.App/Tray/TrayIconController.cs`:

```csharp
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Core.Power;
using KeepAwakeTool.Core.Scheduling;
using KeepAwakeTool.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App.Tray;

public sealed class TrayIconController
{
    private readonly IServiceProvider _sp;
    private TrayIcon? _tray;
    private SettingsWindow? _settingsWindow;

    public TrayIconController(IServiceProvider sp) => _sp = sp;

    public void Initialize()
    {
        var scheduler = _sp.GetRequiredService<Scheduler>();
        scheduler.StateChanged += (_, state) => Dispatcher.UIThread.Post(() => UpdateIcon(state));

        _tray = new TrayIcon
        {
            ToolTipText = "KeepAwakeTool — Stopped",
            IsVisible = true,
            Menu = BuildMenu()
        };
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _tray });
        UpdateIcon(scheduler.State);
        scheduler.Start();
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();
        var pause = new NativeMenuItem("Pause");
        pause.Click += (_, _) => _sp.GetRequiredService<Scheduler>().TogglePause();
        menu.Add(pause);

        menu.Add(new NativeMenuItemSeparator());

        var settings = new NativeMenuItem("Settings…");
        settings.Click += (_, _) => OpenSettings();
        menu.Add(settings);

        menu.Add(new NativeMenuItemSeparator());

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => { (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown(); };
        menu.Add(quit);

        return menu;
    }

    private void OpenSettings()
    {
        if (_settingsWindow is null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new SettingsWindow(_sp);
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void UpdateIcon(EngineState state)
    {
        if (_tray is null) return;
        var asset = state switch
        {
            EngineState.Running   => "avares://KeepAwakeTool/Tray/Assets/icon-running.ico",
            EngineState.Paused    => "avares://KeepAwakeTool/Tray/Assets/icon-paused.ico",
            EngineState.PowerSave => "avares://KeepAwakeTool/Tray/Assets/icon-powersave.ico",
            _                     => "avares://KeepAwakeTool/Tray/Assets/icon-stopped.ico"
        };
        using var stream = AssetLoader.Open(new Uri(asset));
        _tray.Icon = new WindowIcon(stream);
        _tray.ToolTipText = $"KeepAwakeTool — {state}";
    }
}
```

- [ ] **Step 3: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/Tray/ src/KeepAwakeTool.App/KeepAwakeTool.App.csproj
git commit -m "feat(app): TrayIconController with state-driven icon, menu, settings launcher"
```

---

### Task 23: SettingsWindow shell + General tab

**Files:**
- Create: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`
- Create: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs`
- Create: `src/KeepAwakeTool.App/ViewModels/SettingsViewModel.cs`
- Create: `src/KeepAwakeTool.App/ViewModels/GeneralTabViewModel.cs`
- Create: `src/KeepAwakeTool.App/Views/GeneralTab.axaml`
- Create: `src/KeepAwakeTool.App/Views/GeneralTab.axaml.cs`

- [ ] **Step 1: Write SettingsViewModel**

Write `src/KeepAwakeTool.App/ViewModels/SettingsViewModel.cs`:

```csharp
using System;
using System.ComponentModel;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private AppConfig _draft;
    public event PropertyChangedEventHandler? PropertyChanged;

    public GeneralTabViewModel General { get; }
    public ActivityTabViewModel Activity { get; }
    public PowerTabViewModel Power { get; }
    public ScheduleTabViewModel Schedule { get; }
    public HotkeyTabViewModel Hotkey { get; }

    public SettingsViewModel(AppConfig initial)
    {
        _draft = initial;
        General  = new GeneralTabViewModel(initial);
        Activity = new ActivityTabViewModel(initial);
        Power    = new PowerTabViewModel(initial);
        Schedule = new ScheduleTabViewModel(initial);
        Hotkey   = new HotkeyTabViewModel(initial);
    }

    public AppConfig BuildConfig() => _draft with
    {
        Activity = Activity.Build(),
        Power    = Power.Build(),
        Schedule = Schedule.Build(),
        Hotkey   = Hotkey.Build(),
        Startup  = General.BuildStartup(),
        Ui       = General.BuildUi()
    };
}
```

- [ ] **Step 2: Write GeneralTabViewModel**

Write `src/KeepAwakeTool.App/ViewModels/GeneralTabViewModel.cs`:

```csharp
using System.ComponentModel;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class GeneralTabViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public int IntervalSeconds { get; set; }
    public int IdleThresholdSeconds { get; set; }
    public bool AutoStartOnLogin { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public string Theme { get; set; } = "System";

    public GeneralTabViewModel(AppConfig cfg)
    {
        IntervalSeconds = cfg.Activity.IntervalSeconds;
        IdleThresholdSeconds = cfg.Activity.IdleThresholdSeconds;
        AutoStartOnLogin = cfg.Startup.AutoStartOnLogin;
        StartMinimizedToTray = cfg.Startup.StartMinimizedToTray;
        Theme = cfg.Ui.Theme;
    }

    public StartupConfig BuildStartup() => new()
    {
        AutoStartOnLogin = AutoStartOnLogin,
        StartMinimizedToTray = StartMinimizedToTray
    };

    public UiConfig BuildUi() => new() { Theme = Theme };
}
```

- [ ] **Step 3: Stub the other tab viewmodels** (to satisfy compilation; flesh out in later tasks)

Write `src/KeepAwakeTool.App/ViewModels/ActivityTabViewModel.cs`:

```csharp
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ActivityTabViewModel
{
    private readonly AppConfig _src;
    public ActivityTabViewModel(AppConfig cfg) => _src = cfg;
    public ActivityConfig Build() => _src.Activity;
}
```

Write `src/KeepAwakeTool.App/ViewModels/PowerTabViewModel.cs`:

```csharp
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class PowerTabViewModel
{
    private readonly AppConfig _src;
    public PowerTabViewModel(AppConfig cfg) => _src = cfg;
    public PowerConfig Build() => _src.Power;
}
```

Write `src/KeepAwakeTool.App/ViewModels/ScheduleTabViewModel.cs`:

```csharp
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ScheduleTabViewModel
{
    private readonly AppConfig _src;
    public ScheduleTabViewModel(AppConfig cfg) => _src = cfg;
    public ScheduleConfig Build() => _src.Schedule;
}
```

Write `src/KeepAwakeTool.App/ViewModels/HotkeyTabViewModel.cs`:

```csharp
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class HotkeyTabViewModel
{
    private readonly AppConfig _src;
    public HotkeyTabViewModel(AppConfig cfg) => _src = cfg;
    public HotkeyConfig Build() => _src.Hotkey;
}
```

- [ ] **Step 4: Write SettingsWindow XAML**

Write `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:v="using:KeepAwakeTool.App.Views"
        x:Class="KeepAwakeTool.App.Views.SettingsWindow"
        Width="600" Height="450"
        CanResize="False"
        Title="KeepAwakeTool — Settings">
    <DockPanel>
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" HorizontalAlignment="Right" Margin="10">
            <Button Content="Apply" Name="ApplyButton" Margin="0,0,5,0"/>
            <Button Content="OK"    Name="OkButton"    Margin="0,0,5,0"/>
            <Button Content="Cancel" Name="CancelButton"/>
        </StackPanel>
        <TabControl>
            <TabItem Header="General"><v:GeneralTab/></TabItem>
            <TabItem Header="Activity"><TextBlock Margin="10" Text="(Activity tab — Task 24)"/></TabItem>
            <TabItem Header="Power"><TextBlock Margin="10" Text="(Power tab — Task 25)"/></TabItem>
            <TabItem Header="Schedule"><TextBlock Margin="10" Text="(Schedule tab — Task 26)"/></TabItem>
            <TabItem Header="Hotkey"><TextBlock Margin="10" Text="(Hotkey tab — Task 27)"/></TabItem>
        </TabControl>
    </DockPanel>
</Window>
```

Write `src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs`:

```csharp
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KeepAwakeTool.App.ViewModels;
using KeepAwakeTool.Core.Config;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App.Views;

public partial class SettingsWindow : Window
{
    private readonly IServiceProvider _sp;
    private readonly ConfigurationStore _store;
    private readonly SettingsViewModel _vm;

    public SettingsWindow(IServiceProvider sp)
    {
        _sp = sp;
        _store = sp.GetRequiredService<ConfigurationStore>();
        _vm = new SettingsViewModel(_store.Load());
        InitializeComponent();
        DataContext = _vm;

        this.FindControl<Button>("ApplyButton")!.Click += (_, _) => ApplyAndStay();
        this.FindControl<Button>("OkButton")!.Click    += (_, _) => { ApplyAndStay(); Close(); };
        this.FindControl<Button>("CancelButton")!.Click+= (_, _) => Close();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ApplyAndStay() => _store.Save(_vm.BuildConfig());
}
```

- [ ] **Step 5: Write GeneralTab.axaml + code-behind**

Write `src/KeepAwakeTool.App/Views/GeneralTab.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:KeepAwakeTool.App.ViewModels"
             x:Class="KeepAwakeTool.App.Views.GeneralTab"
             x:DataType="vm:SettingsViewModel">
    <StackPanel Margin="15" Spacing="12">
        <StackPanel Spacing="4">
            <TextBlock Text="Interval (seconds)"/>
            <NumericUpDown Minimum="10" Maximum="240" Increment="5" Value="{Binding General.IntervalSeconds}"/>
        </StackPanel>
        <StackPanel Spacing="4">
            <TextBlock Text="Smart-pause idle threshold (seconds)"/>
            <NumericUpDown Minimum="5" Maximum="120" Increment="5" Value="{Binding General.IdleThresholdSeconds}"/>
        </StackPanel>
        <CheckBox Content="Start with Windows" IsChecked="{Binding General.AutoStartOnLogin}"/>
        <CheckBox Content="Start minimized to tray" IsChecked="{Binding General.StartMinimizedToTray}"/>
        <StackPanel Spacing="4">
            <TextBlock Text="Theme"/>
            <ComboBox SelectedItem="{Binding General.Theme}">
                <ComboBoxItem>System</ComboBoxItem>
                <ComboBoxItem>Light</ComboBoxItem>
                <ComboBoxItem>Dark</ComboBoxItem>
            </ComboBox>
        </StackPanel>
    </StackPanel>
</UserControl>
```

Write `src/KeepAwakeTool.App/Views/GeneralTab.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class GeneralTab : UserControl
{
    public GeneralTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 6: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/Views/ src/KeepAwakeTool.App/ViewModels/
git commit -m "feat(app): Settings window shell + General tab bound to ConfigurationStore"
```

---

### Task 24: Activity tab UI

**Files:**
- Modify: `src/KeepAwakeTool.App/ViewModels/ActivityTabViewModel.cs`
- Create: `src/KeepAwakeTool.App/Views/ActivityTab.axaml`
- Create: `src/KeepAwakeTool.App/Views/ActivityTab.axaml.cs`
- Modify: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`

- [ ] **Step 1: Flesh out ActivityTabViewModel**

Replace `src/KeepAwakeTool.App/ViewModels/ActivityTabViewModel.cs`:

```csharp
using System.ComponentModel;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ActivityTabViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public MouseMode MouseMode { get; set; }
    public int JigglePixels { get; set; }
    public bool KeystrokeEnabled { get; set; }
    public VirtualKey KeystrokeKey { get; set; }
    public int EveryNthCycle { get; set; }

    public ActivityTabViewModel(AppConfig cfg)
    {
        MouseMode = cfg.Activity.Mouse.Mode;
        JigglePixels = cfg.Activity.Mouse.JigglePixels;
        KeystrokeEnabled = cfg.Activity.Keystroke.Enabled;
        KeystrokeKey = cfg.Activity.Keystroke.Key;
        EveryNthCycle = cfg.Activity.Keystroke.EveryNthCycle;
    }

    public ActivityConfig Build() => new()
    {
        Mouse = new MouseConfig { Mode = MouseMode, JigglePixels = JigglePixels },
        Keystroke = new KeystrokeConfig { Enabled = KeystrokeEnabled, Key = KeystrokeKey, EveryNthCycle = EveryNthCycle }
    };
}
```

- [ ] **Step 2: Write ActivityTab.axaml + code-behind**

Write `src/KeepAwakeTool.App/Views/ActivityTab.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:KeepAwakeTool.App.ViewModels"
             xmlns:act="using:KeepAwakeTool.Core.Activity"
             xmlns:plat="using:KeepAwakeTool.Core.Platform"
             x:Class="KeepAwakeTool.App.Views.ActivityTab"
             x:DataType="vm:SettingsViewModel">
    <StackPanel Margin="15" Spacing="12">
        <TextBlock Text="Mouse mode" FontWeight="Bold"/>
        <RadioButton Content="Invisible (no cursor movement)" IsChecked="{Binding Activity.MouseMode, Converter={x:Static x:Boolean.Equals}}" GroupName="MouseMode"
                     Tag="{x:Static act:MouseMode.Invisible}"/>
        <RadioButton Content="Jiggle (visible twitch)" GroupName="MouseMode"
                     Tag="{x:Static act:MouseMode.Jiggle}"/>
        <NumericUpDown Minimum="1" Maximum="10" Value="{Binding Activity.JigglePixels}" Width="120" HorizontalAlignment="Left"
                       IsVisible="{Binding Activity.MouseMode, Converter={x:Static x:Boolean.Equals}, ConverterParameter={x:Static act:MouseMode.Jiggle}}"/>

        <Separator/>
        <TextBlock Text="Keystroke" FontWeight="Bold"/>
        <CheckBox Content="Enabled" IsChecked="{Binding Activity.KeystrokeEnabled}"/>
        <StackPanel Spacing="4" IsEnabled="{Binding Activity.KeystrokeEnabled}">
            <TextBlock Text="Key"/>
            <ComboBox SelectedItem="{Binding Activity.KeystrokeKey}" Width="120" HorizontalAlignment="Left">
                <plat:VirtualKey>F13</plat:VirtualKey>
                <plat:VirtualKey>F14</plat:VirtualKey>
                <plat:VirtualKey>F15</plat:VirtualKey>
            </ComboBox>
            <TextBlock Text="Send every Nth cycle"/>
            <NumericUpDown Minimum="1" Maximum="10" Value="{Binding Activity.EveryNthCycle}" Width="120" HorizontalAlignment="Left"/>
        </StackPanel>
    </StackPanel>
</UserControl>
```

Write `src/KeepAwakeTool.App/Views/ActivityTab.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class ActivityTab : UserControl
{
    public ActivityTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 3: Swap the placeholder TabItem**

In `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`, replace:

```xml
            <TabItem Header="Activity"><TextBlock Margin="10" Text="(Activity tab — Task 24)"/></TabItem>
```

with:

```xml
            <TabItem Header="Activity"><v:ActivityTab/></TabItem>
```

- [ ] **Step 4: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/Views/ActivityTab.axaml src/KeepAwakeTool.App/Views/ActivityTab.axaml.cs src/KeepAwakeTool.App/ViewModels/ActivityTabViewModel.cs src/KeepAwakeTool.App/Views/SettingsWindow.axaml
git commit -m "feat(app): Activity settings tab — mouse mode, jiggle, keystroke options"
```

---

### Task 25: Power tab UI

**Files:**
- Modify: `src/KeepAwakeTool.App/ViewModels/PowerTabViewModel.cs`
- Create: `src/KeepAwakeTool.App/Views/PowerTab.axaml`
- Create: `src/KeepAwakeTool.App/Views/PowerTab.axaml.cs`
- Modify: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`

- [ ] **Step 1: Flesh out PowerTabViewModel**

Replace `src/KeepAwakeTool.App/ViewModels/PowerTabViewModel.cs`:

```csharp
using System.ComponentModel;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class PowerTabViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool ForceDisplayOffAfterInjection { get; set; }
    public bool PowerSaveMode { get; set; }

    public PowerTabViewModel(AppConfig cfg)
    {
        ForceDisplayOffAfterInjection = cfg.Power.ForceDisplayOffAfterInjection;
        PowerSaveMode = cfg.Power.PowerSaveMode;
    }

    public PowerConfig Build() => new()
    {
        ForceDisplayOffAfterInjection = ForceDisplayOffAfterInjection,
        PowerSaveMode = PowerSaveMode
    };
}
```

- [ ] **Step 2: Write PowerTab.axaml + code-behind**

Write `src/KeepAwakeTool.App/Views/PowerTab.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:KeepAwakeTool.App.ViewModels"
             x:Class="KeepAwakeTool.App.Views.PowerTab"
             x:DataType="vm:SettingsViewModel">
    <StackPanel Margin="15" Spacing="14">
        <StackPanel Spacing="4">
            <CheckBox Content="Force display off after each injection (S1)" IsChecked="{Binding Power.ForceDisplayOffAfterInjection}"/>
            <TextBlock TextWrapping="Wrap" Foreground="Gray"
                       Text="When enabled, after each synthetic input the display is turned off ~200 ms later. Causes a brief 'blink' each interval. Default OFF."/>
        </StackPanel>
        <StackPanel Spacing="4">
            <CheckBox Content="Power-Save Mode (S3) — disables presence injection" IsChecked="{Binding Power.PowerSaveMode}"/>
            <TextBlock TextWrapping="Wrap" Foreground="Gray"
                       Text="When enabled, the tool keeps the system from sleeping but does NOT inject input. Teams and similar apps will mark you as Away. Default OFF."/>
        </StackPanel>
    </StackPanel>
</UserControl>
```

Write `src/KeepAwakeTool.App/Views/PowerTab.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class PowerTab : UserControl
{
    public PowerTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 3: Replace placeholder**

In `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`, replace:

```xml
            <TabItem Header="Power"><TextBlock Margin="10" Text="(Power tab — Task 25)"/></TabItem>
```

with:

```xml
            <TabItem Header="Power"><v:PowerTab/></TabItem>
```

- [ ] **Step 4: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/Views/PowerTab.axaml src/KeepAwakeTool.App/Views/PowerTab.axaml.cs src/KeepAwakeTool.App/ViewModels/PowerTabViewModel.cs src/KeepAwakeTool.App/Views/SettingsWindow.axaml
git commit -m "feat(app): Power settings tab with S1 and S3 toggles and explainers"
```

---

### Task 26: Schedule tab UI

**Files:**
- Modify: `src/KeepAwakeTool.App/ViewModels/ScheduleTabViewModel.cs`
- Create: `src/KeepAwakeTool.App/Views/ScheduleTab.axaml`
- Create: `src/KeepAwakeTool.App/Views/ScheduleTab.axaml.cs`
- Modify: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`

- [ ] **Step 1: Flesh out ScheduleTabViewModel**

Replace `src/KeepAwakeTool.App/ViewModels/ScheduleTabViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ScheduleTabViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Enabled { get; set; }
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "18:00";
    public bool Mon { get; set; } = true;
    public bool Tue { get; set; } = true;
    public bool Wed { get; set; } = true;
    public bool Thu { get; set; } = true;
    public bool Fri { get; set; } = true;
    public bool Sat { get; set; }
    public bool Sun { get; set; }

    public ScheduleTabViewModel(AppConfig cfg)
    {
        Enabled = cfg.Schedule.Enabled;
        StartTime = cfg.Schedule.StartTime;
        EndTime = cfg.Schedule.EndTime;
        var days = cfg.Schedule.Days;
        Mon = days.Contains(DayOfWeek.Monday);
        Tue = days.Contains(DayOfWeek.Tuesday);
        Wed = days.Contains(DayOfWeek.Wednesday);
        Thu = days.Contains(DayOfWeek.Thursday);
        Fri = days.Contains(DayOfWeek.Friday);
        Sat = days.Contains(DayOfWeek.Saturday);
        Sun = days.Contains(DayOfWeek.Sunday);
    }

    public ScheduleConfig Build()
    {
        var days = new List<DayOfWeek>();
        if (Mon) days.Add(DayOfWeek.Monday);
        if (Tue) days.Add(DayOfWeek.Tuesday);
        if (Wed) days.Add(DayOfWeek.Wednesday);
        if (Thu) days.Add(DayOfWeek.Thursday);
        if (Fri) days.Add(DayOfWeek.Friday);
        if (Sat) days.Add(DayOfWeek.Saturday);
        if (Sun) days.Add(DayOfWeek.Sunday);
        return new ScheduleConfig
        {
            Enabled = Enabled, StartTime = StartTime, EndTime = EndTime, Days = days
        };
    }
}
```

- [ ] **Step 2: Write ScheduleTab.axaml**

Write `src/KeepAwakeTool.App/Views/ScheduleTab.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:KeepAwakeTool.App.ViewModels"
             x:Class="KeepAwakeTool.App.Views.ScheduleTab"
             x:DataType="vm:SettingsViewModel">
    <StackPanel Margin="15" Spacing="10">
        <CheckBox Content="Enable working-hours schedule" IsChecked="{Binding Schedule.Enabled}"/>
        <StackPanel IsEnabled="{Binding Schedule.Enabled}" Spacing="10">
            <StackPanel Orientation="Horizontal" Spacing="10">
                <StackPanel>
                    <TextBlock Text="Start time (HH:mm)"/>
                    <TextBox Text="{Binding Schedule.StartTime}" Width="100"/>
                </StackPanel>
                <StackPanel>
                    <TextBlock Text="End time (HH:mm)"/>
                    <TextBox Text="{Binding Schedule.EndTime}" Width="100"/>
                </StackPanel>
            </StackPanel>
            <TextBlock Text="Days"/>
            <StackPanel Orientation="Horizontal" Spacing="8">
                <CheckBox Content="Mon" IsChecked="{Binding Schedule.Mon}"/>
                <CheckBox Content="Tue" IsChecked="{Binding Schedule.Tue}"/>
                <CheckBox Content="Wed" IsChecked="{Binding Schedule.Wed}"/>
                <CheckBox Content="Thu" IsChecked="{Binding Schedule.Thu}"/>
                <CheckBox Content="Fri" IsChecked="{Binding Schedule.Fri}"/>
                <CheckBox Content="Sat" IsChecked="{Binding Schedule.Sat}"/>
                <CheckBox Content="Sun" IsChecked="{Binding Schedule.Sun}"/>
            </StackPanel>
        </StackPanel>
    </StackPanel>
</UserControl>
```

Write `src/KeepAwakeTool.App/Views/ScheduleTab.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class ScheduleTab : UserControl
{
    public ScheduleTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 3: Replace placeholder**

In `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`, replace:

```xml
            <TabItem Header="Schedule"><TextBlock Margin="10" Text="(Schedule tab — Task 26)"/></TabItem>
```

with:

```xml
            <TabItem Header="Schedule"><v:ScheduleTab/></TabItem>
```

- [ ] **Step 4: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/Views/ScheduleTab.axaml src/KeepAwakeTool.App/Views/ScheduleTab.axaml.cs src/KeepAwakeTool.App/ViewModels/ScheduleTabViewModel.cs src/KeepAwakeTool.App/Views/SettingsWindow.axaml
git commit -m "feat(app): Schedule settings tab — time range and weekday selection"
```

---

### Task 27: Hotkey tab UI

**Files:**
- Modify: `src/KeepAwakeTool.App/ViewModels/HotkeyTabViewModel.cs`
- Create: `src/KeepAwakeTool.App/Views/HotkeyTab.axaml`
- Create: `src/KeepAwakeTool.App/Views/HotkeyTab.axaml.cs`
- Modify: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`

- [ ] **Step 1: Flesh out HotkeyTabViewModel**

Replace `src/KeepAwakeTool.App/ViewModels/HotkeyTabViewModel.cs`:

```csharp
using System.ComponentModel;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class HotkeyTabViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool Enabled { get; set; }
    public string Combination { get; set; } = "Ctrl+Alt+P";
    public string ValidationMessage { get; set; } = string.Empty;

    public HotkeyTabViewModel(AppConfig cfg)
    {
        Enabled = cfg.Hotkey.Enabled;
        Combination = cfg.Hotkey.Combination;
    }

    public HotkeyConfig Build() => new() { Enabled = Enabled, Combination = Combination };
}
```

- [ ] **Step 2: Write HotkeyTab.axaml**

Write `src/KeepAwakeTool.App/Views/HotkeyTab.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:KeepAwakeTool.App.ViewModels"
             x:Class="KeepAwakeTool.App.Views.HotkeyTab"
             x:DataType="vm:SettingsViewModel">
    <StackPanel Margin="15" Spacing="10">
        <CheckBox Content="Enable global hotkey to pause/resume" IsChecked="{Binding Hotkey.Enabled}"/>
        <StackPanel IsEnabled="{Binding Hotkey.Enabled}" Spacing="6">
            <TextBlock Text="Combination (e.g. Ctrl+Alt+P)"/>
            <TextBox Text="{Binding Hotkey.Combination}" Width="240" HorizontalAlignment="Left"/>
            <TextBlock Text="{Binding Hotkey.ValidationMessage}" Foreground="OrangeRed"/>
        </StackPanel>
    </StackPanel>
</UserControl>
```

Write `src/KeepAwakeTool.App/Views/HotkeyTab.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class HotkeyTab : UserControl
{
    public HotkeyTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

- [ ] **Step 3: Replace placeholder**

In `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`, replace:

```xml
            <TabItem Header="Hotkey"><TextBlock Margin="10" Text="(Hotkey tab — Task 27)"/></TabItem>
```

with:

```xml
            <TabItem Header="Hotkey"><v:HotkeyTab/></TabItem>
```

- [ ] **Step 4: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/Views/HotkeyTab.axaml src/KeepAwakeTool.App/Views/HotkeyTab.axaml.cs src/KeepAwakeTool.App/ViewModels/HotkeyTabViewModel.cs src/KeepAwakeTool.App/Views/SettingsWindow.axaml
git commit -m "feat(app): Hotkey settings tab — combination input with validation slot"
```

---

### Task 28: Engine pump + Apply-time side effects (autostart, hotkey, power-mode)

**Files:**
- Create: `src/KeepAwakeTool.App/EnginePump.cs`
- Modify: `src/KeepAwakeTool.App/App.axaml.cs`
- Modify: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs`

- [ ] **Step 1: Write EnginePump (drives the periodic tick)**

Write `src/KeepAwakeTool.App/EnginePump.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App;

public sealed class EnginePump : IDisposable
{
    private readonly IServiceProvider _sp;
    private readonly Scheduler _scheduler;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public EnginePump(IServiceProvider sp)
    {
        _sp = sp;
        _scheduler = sp.GetRequiredService<Scheduler>();
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var configProvider = _sp.GetRequiredService<Func<AppConfig>>();
        while (!ct.IsCancellationRequested)
        {
            var cfg = configProvider();
            var interval = TimeSpan.FromSeconds(Math.Max(10, cfg.Activity.IntervalSeconds));
            try { await Task.Delay(interval, ct); }
            catch (TaskCanceledException) { break; }
            try { await _scheduler.RunOneTickAsync(ct); }
            catch (Exception) { /* logged in Task 30 */ }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loop?.Wait(2000); } catch { }
        _cts?.Dispose();
    }
}
```

- [ ] **Step 2: Start the pump and hook power-controller on App startup**

In `src/KeepAwakeTool.App/App.axaml.cs`, replace `OnFrameworkInitializationCompleted`:

```csharp
    private EnginePump? _pump;

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServiceRegistration.Build();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var power = Services.GetRequiredService<Core.Power.PowerModeController>();
            var configProvider = Services.GetRequiredService<Func<Core.Config.AppConfig>>();
            power.Start(configProvider().Power);

            desktop.Exit += (_, _) =>
            {
                _pump?.Dispose();
                power.Stop();
            };

            Tray = new TrayIconController(Services);
            Tray.Initialize();

            _pump = new EnginePump(Services);
            _pump.Start();

            ApplyAutostart(configProvider());
            ApplyHotkey(configProvider());
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyAutostart(Core.Config.AppConfig cfg)
    {
        var mgr = Services.GetRequiredService<Core.Platform.IAutoStartManager>();
        if (cfg.Startup.AutoStartOnLogin && !mgr.IsEnabled) mgr.Enable();
        else if (!cfg.Startup.AutoStartOnLogin && mgr.IsEnabled) mgr.Disable();
    }

    private void ApplyHotkey(Core.Config.AppConfig cfg)
    {
        var hk = Services.GetRequiredService<Core.Platform.IGlobalHotkeyService>();
        hk.Unregister();
        if (!cfg.Hotkey.Enabled) return;
        var scheduler = Services.GetRequiredService<Scheduler>();
        try
        {
            var hotkey = Core.Hotkey.Hotkey.Parse(cfg.Hotkey.Combination);
            hk.TryRegister(hotkey, () => Avalonia.Threading.Dispatcher.UIThread.Post(scheduler.TogglePause));
        }
        catch (ArgumentException) { /* invalid combination — skip */ }
    }
```

Add the requisite using directives at the top: `using KeepAwakeTool.Core.Scheduling;`, `using Microsoft.Extensions.DependencyInjection;`, `using System;`.

- [ ] **Step 3: Re-apply autostart and hotkey on settings Apply**

In `src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs`, replace `ApplyAndStay`:

```csharp
    private void ApplyAndStay()
    {
        var cfg = _vm.BuildConfig();
        _store.Save(cfg);

        var app = (App)Avalonia.Application.Current!;
        app.GetType()
            .GetMethod("ApplyAutostart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(app, new object?[] { cfg });
        app.GetType()
            .GetMethod("ApplyHotkey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(app, new object?[] { cfg });
    }
```

(A reflection call is fine here as a v1 shortcut. Refactor to a dedicated `IAppSettings` service post-v1 if it grows.)

- [ ] **Step 4: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.App/EnginePump.cs src/KeepAwakeTool.App/App.axaml.cs src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs
git commit -m "feat(app): pump engine ticks, apply autostart/hotkey on save"
```

---

### Task 29: System sleep/wake re-arm + FileLogger + global exception handler

**Files:**
- Create: `src/KeepAwakeTool.Core/Diagnostics/FileLogger.cs`
- Modify: `src/KeepAwakeTool.App/Program.cs`
- Modify: `src/KeepAwakeTool.App/App.axaml.cs`

- [ ] **Step 1: Write FileLogger**

Write `src/KeepAwakeTool.Core/Diagnostics/FileLogger.cs`:

```csharp
using System;
using System.IO;

namespace KeepAwakeTool.Core.Diagnostics;

public sealed class FileLogger
{
    private readonly object _gate = new();
    private readonly string _dir;

    public FileLogger(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    public void Log(string level, string message)
    {
        var path = Path.Combine(_dir, $"keepawaketool-{DateTime.UtcNow:yyyyMMdd}.log");
        var line = $"{DateTime.UtcNow:O} [{level}] {message}{Environment.NewLine}";
        lock (_gate) File.AppendAllText(path, line);
    }
}
```

- [ ] **Step 2: Install global exception handler in Program.cs**

In `src/KeepAwakeTool.App/Program.cs`, at the top of `Main`, before the lifetime start, add:

```csharp
        var logger = new KeepAwakeTool.Core.Diagnostics.FileLogger(
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "KeepAwakeTool", "logs"));

        System.AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Log("FATAL", e.ExceptionObject?.ToString() ?? "unknown");
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Log("ERROR", e.Exception.ToString());
            e.SetObserved();
        };
```

- [ ] **Step 3: Re-arm engine after sleep/wake (Windows-only event)**

In `src/KeepAwakeTool.App/App.axaml.cs`, inside `OnFrameworkInitializationCompleted` after `_pump.Start();`, append:

```csharp
            Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) =>
            {
                if (e.Mode == Microsoft.Win32.PowerModes.Resume)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Services.GetRequiredService<Core.Power.PowerModeController>()
                            .Start(configProvider().Power);
                    });
                }
            };
```

Add `<PackageReference Include="Microsoft.Win32.SystemEvents" Version="10.0.0" />` to `KeepAwakeTool.App.csproj`.

- [ ] **Step 4: Build & commit**

```bash
dotnet build src/KeepAwakeTool.App
git add src/KeepAwakeTool.Core/Diagnostics/FileLogger.cs src/KeepAwakeTool.App/Program.cs src/KeepAwakeTool.App/App.axaml.cs src/KeepAwakeTool.App/KeepAwakeTool.App.csproj
git commit -m "feat(app): file logger, global exception handler, sleep/wake re-arm"
```

---

## Phase 8 — Docs & CI

### Task 30: Manual smoke-test plan

**Files:**
- Create: `docs/manual-smoke.md`

- [ ] **Step 1: Write the smoke test doc**

Write `docs/manual-smoke.md`:

```markdown
# KeepAwakeTool — Manual Smoke Test Plan (Windows v1)

Run after each release build (`dotnet publish -c Release -r win-x64 --self-contained`).

## Pre-flight
- Fresh log in to a Windows 11 box with Microsoft Teams installed.
- No prior KeepAwakeTool config in `%AppData%\KeepAwakeTool\`.

## 1. First run / defaults
1. Launch the published `KeepAwakeTool.exe`.
2. Expect: tray icon appears, color = green (Running).
3. Expect: `%AppData%\KeepAwakeTool\config.json` exists with defaults.

## 2. Presence with default config
1. Open Teams; sign in.
2. Do not touch keyboard/mouse for 15 minutes.
3. Expect: Teams status stays "Available" throughout.

## 3. Smart-pause
1. Open Settings → General; verify idle threshold is 30 s.
2. Watch a clock; type a single key, then wait.
3. Expect: no synthetic activity for at least 30 s after your keystroke (no cursor twitches, no F-key event in a key logger if instrumented).

## 4. S1 (Force display off)
1. Settings → Power → enable "Force display off after each injection".
2. Apply.
3. Within one interval, the display turns off; ~200 ms after the next synthetic input the display flickers off again. Expect a brief blink each interval.

## 5. S3 (Power-Save Mode)
1. Settings → Power → enable "Power-Save Mode".
2. Apply.
3. Tray icon turns red. No mouse/keyboard input is injected. System still does not sleep (verify by waiting beyond the OS power-plan sleep timer).
4. Teams will mark you Away after its normal threshold. This is expected.

## 6. Global hotkey
1. Settings → Hotkey → enable; set "Ctrl+Alt+P". Apply.
2. From any focused app, press Ctrl+Alt+P.
3. Tray icon turns yellow (Paused). Press again — returns to green.

## 7. Working hours schedule
1. Settings → Schedule → enable; set Start = current time + 2 minutes, End = current time + 4 minutes.
2. Apply.
3. Before start: tray reflects no activity. After start: activity resumes. After end: stops again.

## 8. Autostart
1. Settings → General → enable "Start with Windows". Apply.
2. Confirm registry value `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\KeepAwakeTool` points to the exe.
3. Reboot.
4. Expect: tray icon appears within ~10 s of login.

## 9. Single instance
1. With the app running, launch the exe again.
2. Expect: second instance exits immediately; original tray icon remains.

## 10. Crash safety
1. Force-kill `KeepAwakeTool.exe` via Task Manager.
2. Expect: no Windows sleep block remains; ES_CONTINUOUS clears.
3. Logs under `%AppData%\KeepAwakeTool\logs\keepawaketool-YYYYMMDD.log` are present and readable.
```

- [ ] **Step 2: Commit**

```bash
git add docs/manual-smoke.md
git commit -m "docs: manual smoke-test plan for Windows v1"
```

---

### Task 31: GitHub Actions CI

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Write the CI workflow**

Write `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --verbosity normal

  publish:
    needs: build-test
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet publish src/KeepAwakeTool.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
      - uses: actions/upload-artifact@v4
        with:
          name: KeepAwakeTool-win-x64
          path: publish/
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "build: GitHub Actions CI — restore, build, test, publish win-x64 single file"
```

---

## Self-Review (against spec)

Checked the plan vs `docs/superpowers/specs/2026-05-14-keepawaketool-design.md`:

- **§4 Tech Stack** — net10.0, Avalonia 12, DI, System.Text.Json, xUnit + NSubstitute + FluentAssertions: ✅ Task 1–2.
- **§5 Solution layout** — four projects (Core, Platform.Win, App, Core.Tests): ✅ Task 2.
- **§5.1 Interfaces** — IInputSimulator/IPowerManager/IIdleMonitor/IAutoStartManager/IGlobalHotkeyService: ✅ Task 7. Note: `IInputSimulator.MoveMouse` includes `jigglePixels` so callers don't have to repeat config — minor signature refinement vs spec.
- **§5.2 Core components** — ActivityEngine, PowerModeController, ConfigurationStore, Scheduler: ✅ Tasks 4–13.
- **§5.3 Platform.Win impls** — Input, Power, Idle, AutoStart, Hotkey: ✅ Tasks 14–19.
- **§5.4 App** — Avalonia App, DI, single-instance, tray, settings: ✅ Tasks 20–28.
- **§6 Tick decision tree** — S3, schedule, hotkey, idle, mouse, every-Nth key, S1 delay: ✅ Tasks 8–10, 12.
- **§7 State machine** — Stopped / Running / Paused / PowerSave with stateChanged event: ✅ Tasks 12, 22.
- **§8 Config schema** — record graph + atomic write + watcher + corruption quarantine + clamping: ✅ Tasks 3–6.
- **§9 UI** — tray with state-driven icon + menu, 5 tabs, Apply/OK/Cancel: ✅ Tasks 22–28. Working-hours schedule transition (`§7` "schedule boundary crossings") is realized by `Scheduler.RunOneTickAsync` re-evaluating each tick — Tasks 12 + 28.
- **§10 Error handling** — native API failure tolerance is implicit (`SendInput` ignored, hotkey returns false), corruption quarantine in Task 6, sleep/wake re-arm in Task 29, global exception handler in Task 29.
- **§10.2 RDP warning** — spec mentions surfacing a warning when `GetSystemMetrics(SM_REMOTESESSION)` is true. **Not yet covered.** Adding follow-up note: this is a low-priority polish item for the tray tooltip; can ship v1 without.
- **§11 Testing** — Unit tests cover the full Core decision tree (Tasks 4–13). Platform integration tests are deferred per the spec's "[Trait] PlatformIntegration" gating — not included as a separate task since the spec explicitly marks them opt-in.
- **§12 Build/publish** — `dotnet publish -c Release -r win-x64 --self-contained` ✅ Task 31.
- **§13 Acceptance criteria** — manual smoke-test plan ✅ Task 30.

**Type/name consistency check** — `ActivityEngine`, `Scheduler`, `PowerModeController`, `ConfigurationStore`, `AppConfig`, `MouseMode`, `VirtualKey`, `EngineState`, `Hotkey`, `WindowsInputSimulator`, etc. — names are consistent across all tasks (Tasks 7–28 all use the same identifiers).

**Placeholder scan** — clean. The placeholder TabItems in Task 23 (e.g. "Activity tab — Task 24") are intentional UI scaffolding that the very next task replaces, not plan placeholders.

**Missing-from-spec items added** —
- `IClock` abstraction (not in spec) — added to make `Task.Delay` testable; allows TDD of the 200 ms display-off ordering. Small interface, justifiable scope.
- `EnginePump` (not named in spec) — needed to drive `Scheduler.RunOneTickAsync` on a schedule from `App`; spec §5.2 implies this exists ("owns a `PeriodicTimer`") but does not name the class.

No other gaps. Plan is ready to execute.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-14-keepawaketool.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
