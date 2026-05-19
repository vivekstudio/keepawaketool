# KeepAwakeTool v2 (macOS) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port KeepAwakeTool to macOS (v2) behind the existing platform-abstraction layer, without changing the shipped Windows v1 behavior.

**Architecture:** App project becomes a neutral `net10.0` executable with conditional `ProjectReference`s to `Platform.Win` (Windows/no-RID) and a new `Platform.Mac` (macOS/no-RID); DI selects implementations by `RuntimeInformation.IsOSPlatform`. Three new Core abstractions (`ISystemPowerEvents`, `ISingleInstanceGuard`, `IPermissionGate`) remove the App's direct Windows-only API use. `Platform.Mac` implements the six existing platform interfaces plus the new abstractions via CoreGraphics/IOKit/Carbon/SMAppService P/Invoke. A non-blocking Accessibility banner reuses the existing window-level `StatusBanner`.

**Tech Stack:** .NET 10, C#, Avalonia 12, xUnit/FluentAssertions/NSubstitute, macOS system frameworks via `[LibraryImport]`/`[DllImport]`.

**Spec:** `docs/superpowers/specs/2026-05-18-keepawaketool-v2-macos-design.md` (commit `feb0c58`).

**Conventions (apply to every commit):**
- Conventional commits (`feat:`/`fix:`/`docs:`/`refactor:`/`chore:`/`test:`), AI co-author trailer:
  `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>`
- Git identity is already set locally (`vivekstudio` / `vkc9191@gmail.com`). Do **not** use `--global`.
- Build: `dotnet build -c Release`
- Test: `dotnet test -c Release -- xUnit.ParallelizeTestCollections=false`
  (the non-parallel flag is mandatory — a `FileSystemWatcher` test flakes under parallel collections).
- Use the exact commit command form:
  ```bash
  git commit -m "$(cat <<'EOF'
  <message>

  Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
  EOF
  )"
  ```

**Repo-wide build constraints (root `Directory.Build.props` — applies to every project):**
`<TargetFramework>net10.0</TargetFramework>`, `LangVersion latest`, `Nullable enable`,
`ImplicitUsings enable`, **`TreatWarningsAsErrors=true`** (`WarningsNotAsErrors=NU1903`),
`InvariantGlobalization=true`. Therefore:
- Projects and **test** projects must NOT set `<TargetFramework>` (inherited) — match the
  existing `tests/KeepAwakeTool.Core.Tests/KeepAwakeTool.Core.Tests.csproj` pattern.
- All native interop uses classic **`[DllImport]`** (not `[LibraryImport]`): the
  `LibraryImport` source generator does not support the delegate parameters needed for
  Carbon `InstallEventHandler` / the IOKit power callback, and under
  `TreatWarningsAsErrors` that is a hard failure. `SYSLIB1054` is info-severity and does
  not break the build.
- Keep code warning-clean (no unused private fields → CS0169/CS0649 become errors).
- Pinned test packages (match Core.Tests exactly): `xunit` 2.9.3,
  `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk` 18.5.1,
  `FluentAssertions` **7.2.2** (the team deliberately pins the last free-licensed major;
  test-only, never shipped — CLAUDE.md's "v8" note is stale).

**Baseline before starting:** `dotnet build -c Release` succeeds and `dotnet test -c Release -- xUnit.ParallelizeTestCollections=false` shows **40 Core + 11 App = 51 passing** on macOS.

---

## File Structure

**Phase 1 — restructure + Core abstractions (Windows impls)**
- Create `src/KeepAwakeTool.Core/Platform/IPermissionGate.cs` — input-permission gate interface + always-allow default.
- Create `src/KeepAwakeTool.Core/Platform/ISystemPowerEvents.cs` — resume-from-sleep abstraction.
- Create `src/KeepAwakeTool.Core/Platform/ISingleInstanceGuard.cs` — cross-platform single-instance abstraction.
- Create `src/KeepAwakeTool.Core/Platform/IInputPermissionPrompt.cs` — one-time first-launch permission prompt abstraction.
- Modify `src/KeepAwakeTool.Core/Activity/ActivityEngine.cs` — add optional `IPermissionGate` injection gate.
- Create `tests/KeepAwakeTool.Core.Tests/Fakes/FakePermissionGate.cs` — test double.
- Modify `tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs` — gate tests.
- Create `src/KeepAwakeTool.Platform.Win/WindowsSystemPowerEvents.cs` — wraps `Microsoft.Win32.SystemEvents`.
- Create `src/KeepAwakeTool.Platform.Win/WindowsSingleInstanceGuard.cs` — the named-`Mutex` logic.
- Create `src/KeepAwakeTool.Platform.Win/WindowsPermissionGate.cs` — always-true.
- Create `src/KeepAwakeTool.Platform.Win/WindowsInputPermissionPrompt.cs` — no-op.
- Modify `src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj` — add `Microsoft.Win32.SystemEvents` package.
- Create `src/KeepAwakeTool.Platform.Mac/KeepAwakeTool.Platform.Mac.csproj` — new project (stub in Phase 1).
- Modify `src/KeepAwakeTool.App/KeepAwakeTool.App.csproj` — neutral TFM, conditional refs/manifest.
- Modify `KeepAwakeTool.slnx` — add `Platform.Mac`.
- Modify `src/KeepAwakeTool.App/Composition/ServiceRegistration.cs` — OS-switched registration.
- Modify `src/KeepAwakeTool.App/App.axaml.cs` — consume `ISystemPowerEvents`.
- Modify `src/KeepAwakeTool.App/Program.cs` — consume `ISingleInstanceGuard`.
- Delete `src/KeepAwakeTool.App/SingleInstance/SingleInstanceGuard.cs` — logic moved to Platform.Win.

**Phase 2 — Platform.Mac implementations**
- Create `src/KeepAwakeTool.Platform.Mac/Interop/CoreGraphics.cs`, `Interop/IOKit.cs`, `Interop/Carbon.cs`, `Interop/ObjC.cs`, `Interop/CoreFoundation.cs` — P/Invoke bindings.
- Create `src/KeepAwakeTool.Platform.Mac/MacIdleMonitor.cs`, `MacInputSimulator.cs`, `MacPowerManager.cs`, `MacAccessibility.cs`, `MacPermissionGate.cs`, `MacInputPermissionPrompt.cs`, `MacAutoStartManager.cs`, `MacSessionInfo.cs`, `MacSystemPowerEvents.cs`, `MacSingleInstanceGuard.cs`, `MacGlobalHotkeyService.cs`, `RunLoopThread.cs`.
- Create `tests/KeepAwakeTool.Platform.Mac.Tests/KeepAwakeTool.Platform.Mac.Tests.csproj` + `MacPlatformIntegrationTests.cs`.
- Modify `ServiceRegistration.cs` / `Program.cs` — macOS branch.
- Modify `KeepAwakeTool.slnx` — add Mac tests project.

**Phase 3 — Accessibility banner UX**
- Modify `src/KeepAwakeTool.App/Views/SettingsWindow.axaml` + `.axaml.cs` — banner warning state + buttons.
- Modify `src/KeepAwakeTool.App/Tray/TrayIconController.cs` — gray icon + tooltip suffix when untrusted.

**Phase 4 — docs + CI**
- Create `docs/manual-smoke-macos.md`.
- Modify `.github/workflows/ci.yml` — `macos-latest` job.

---

# Phase 1 — Build restructure + Core abstractions

Goal: solution builds on macOS, all 51 existing tests + new gate tests pass, Windows publish output unchanged.

## Task 1: `IPermissionGate` + ActivityEngine injection gate

**Files:**
- Create: `src/KeepAwakeTool.Core/Platform/IPermissionGate.cs`
- Modify: `src/KeepAwakeTool.Core/Activity/ActivityEngine.cs`
- Create: `tests/KeepAwakeTool.Core.Tests/Fakes/FakePermissionGate.cs`
- Test: `tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs`

- [ ] **Step 1: Create the interface and an always-allow default**

`src/KeepAwakeTool.Core/Platform/IPermissionGate.cs`:

```csharp
namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// Whether the OS currently permits synthetic input injection.
/// Windows has no analog (always true). macOS requires Accessibility trust.
/// Re-evaluated on every access so a runtime permission grant is picked up
/// on the next pump tick without an app restart.
/// </summary>
public interface IPermissionGate
{
    bool CanInjectInput { get; }
}

/// <summary>Default gate used when no platform gate is supplied (always permits).</summary>
public sealed class AlwaysAllowPermissionGate : IPermissionGate
{
    public bool CanInjectInput => true;
}
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs` (inside the class, after the existing fields add a field, and append the two tests):

Add field near the other fakes (after line `private readonly FakePowerManager _power = new();`):

```csharp
    private readonly FakePermissionGate _gate = new();
```

Change `BuildEngine` to pass the gate:

```csharp
    private ActivityEngine BuildEngine(AppConfig? cfg = null)
        => new(_input, _idle, _power, _clock, cfg ?? ConfigDefaults.Default(), _gate);
```

Append these tests:

```csharp
    [Fact]
    public async Task Tick_skips_when_permission_gate_denies_input()
    {
        _gate.CanInjectInput = false;
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().BeEmpty();
    }

    [Fact]
    public async Task Tick_injects_when_permission_gate_allows_input()
    {
        _gate.CanInjectInput = true;
        var engine = BuildEngine();
        await engine.TickAsync(CancellationToken.None);
        _input.MouseMoves.Should().ContainSingle();
    }
```

Create `tests/KeepAwakeTool.Core.Tests/Fakes/FakePermissionGate.cs`:

```csharp
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakePermissionGate : IPermissionGate
{
    public bool CanInjectInput { get; set; } = true;
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test -c Release --filter "FullyQualifiedName~ActivityEngineTests" -- xUnit.ParallelizeTestCollections=false`
Expected: COMPILE FAILURE (ActivityEngine has no 6-arg constructor / `FakePermissionGate` unused param) — this confirms the test targets the new behavior.

- [ ] **Step 4: Add the gate to ActivityEngine**

Modify `src/KeepAwakeTool.Core/Activity/ActivityEngine.cs`. Add a field, an optional constructor parameter, and a gate check as the **last** early-return before injection:

Replace the field block + constructor:

```csharp
    private readonly IInputSimulator _input;
    private readonly IIdleMonitor _idle;
    private readonly IPowerManager _power;
    private readonly IClock _clock;
    private readonly IPermissionGate _gate;
    private AppConfig _config;
    private long _injectionCount;

    public ActivityEngine(IInputSimulator input, IIdleMonitor idle, IPowerManager power,
        IClock clock, AppConfig config, IPermissionGate? permissionGate = null)
    {
        _input = input; _idle = idle; _power = power; _clock = clock; _config = config;
        _gate = permissionGate ?? new AlwaysAllowPermissionGate();
    }
```

In `TickAsync`, add the gate check immediately after the idle check (after the `_idle.TimeSinceLastUserInput()` line, before `_input.MoveMouse(...)`):

```csharp
        if (!_gate.CanInjectInput) return;
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test -c Release --filter "FullyQualifiedName~ActivityEngineTests" -- xUnit.ParallelizeTestCollections=false`
Expected: PASS (all `ActivityEngineTests` including the two new ones).

- [ ] **Step 6: Run the full suite to confirm no regressions**

Run: `dotnet test -c Release -- xUnit.ParallelizeTestCollections=false`
Expected: 53 passing (51 original + 2 new). The optional ctor param keeps `SchedulerTests` / `ActivityEngineInjectedTests` call sites compiling unchanged.

- [ ] **Step 7: Commit**

```bash
git add src/KeepAwakeTool.Core/Platform/IPermissionGate.cs src/KeepAwakeTool.Core/Activity/ActivityEngine.cs tests/KeepAwakeTool.Core.Tests/Fakes/FakePermissionGate.cs tests/KeepAwakeTool.Core.Tests/ActivityEngineTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add IPermissionGate and gate ActivityEngine injection on it

Optional ctor param (defaults to always-allow) so existing call sites are
unaffected; macOS will inject a Accessibility-trust-backed gate via DI.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 2: `ISystemPowerEvents` + Windows implementation

**Files:**
- Create: `src/KeepAwakeTool.Core/Platform/ISystemPowerEvents.cs`
- Create: `src/KeepAwakeTool.Platform.Win/WindowsSystemPowerEvents.cs`
- Modify: `src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj`

- [ ] **Step 1: Create the interface**

`src/KeepAwakeTool.Core/Platform/ISystemPowerEvents.cs`:

```csharp
using System;

namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// Raises <see cref="Resumed"/> when the machine wakes from sleep, so the
/// awake assertion can be re-armed. Windows: SystemEvents.PowerModeChanged.
/// macOS: IOKit IORegisterForSystemPower.
/// </summary>
public interface ISystemPowerEvents : IDisposable
{
    event Action? Resumed;
    void Start();
    void Stop();
}
```

- [ ] **Step 2: Add the Windows-only package reference to Platform.Win**

`Microsoft.Win32.SystemEvents` is currently referenced from `App`; it moves here (it is Windows-only and Platform.Win is `net10.0-windows`). Modify `src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj` — add inside the existing `<ItemGroup>`:

```xml
    <PackageReference Include="Microsoft.Win32.SystemEvents" Version="10.0.8" />
```

- [ ] **Step 3: Implement the Windows adapter**

`src/KeepAwakeTool.Platform.Win/WindowsSystemPowerEvents.cs`:

```csharp
using System;
using Microsoft.Win32;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsSystemPowerEvents : ISystemPowerEvents
{
    public event Action? Resumed;
    private bool _started;

    public void Start()
    {
        if (_started) return;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _started = false;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Resumed?.Invoke();
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors. (Windows project compiles on macOS via the `net10.0-windows` reference assemblies — this matches the established baseline.)

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Core/Platform/ISystemPowerEvents.cs src/KeepAwakeTool.Platform.Win/WindowsSystemPowerEvents.cs src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj
git commit -m "$(cat <<'EOF'
feat(platform-win): add ISystemPowerEvents abstraction + Windows adapter

Moves the Microsoft.Win32.SystemEvents dependency out of the App project
(it is Windows-only) and behind a Core interface.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 3: `ISingleInstanceGuard` + Windows implementation

**Files:**
- Create: `src/KeepAwakeTool.Core/Platform/ISingleInstanceGuard.cs`
- Create: `src/KeepAwakeTool.Platform.Win/WindowsSingleInstanceGuard.cs`

- [ ] **Step 1: Create the interface**

`src/KeepAwakeTool.Core/Platform/ISingleInstanceGuard.cs`:

```csharp
namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// Single-instance enforcement. Windows: named Mutex. macOS: exclusive lock file
/// (.NET named mutexes are process-local on macOS so cannot work cross-instance).
/// </summary>
public interface ISingleInstanceGuard
{
    /// <summary>True if this process acquired the single-instance token.</summary>
    bool TryAcquire();
    /// <summary>v1 no-op; the running tray icon is the "already running" signal.</summary>
    void SignalExistingInstance();
    void Release();
}
```

- [ ] **Step 2: Implement the Windows guard (moves the existing mutex logic verbatim)**

`src/KeepAwakeTool.Platform.Win/WindowsSingleInstanceGuard.cs`:

```csharp
using System.Threading;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsSingleInstanceGuard : ISingleInstanceGuard
{
    private const string MutexName = @"Global\KeepAwakeTool";
    private Mutex? _mutex;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out var createdNew);
        return createdNew;
    }

    public void SignalExistingInstance()
    {
        // v1: rely on tray icon being present; named-pipe focus message can be added later.
    }

    public void Release()
    {
        try { _mutex?.ReleaseMutex(); } catch { /* may have been abandoned */ }
        _mutex?.Dispose();
        _mutex = null;
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/KeepAwakeTool.Core/Platform/ISingleInstanceGuard.cs src/KeepAwakeTool.Platform.Win/WindowsSingleInstanceGuard.cs
git commit -m "$(cat <<'EOF'
feat(platform-win): add ISingleInstanceGuard + Windows mutex implementation

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 4: `WindowsPermissionGate` + `IInputPermissionPrompt` (Core) + Windows no-op

**Files:**
- Create: `src/KeepAwakeTool.Platform.Win/WindowsPermissionGate.cs`
- Create: `src/KeepAwakeTool.Core/Platform/IInputPermissionPrompt.cs`
- Create: `src/KeepAwakeTool.Platform.Win/WindowsInputPermissionPrompt.cs`

- [ ] **Step 1: Implement the Windows gate**

`src/KeepAwakeTool.Platform.Win/WindowsPermissionGate.cs`:

```csharp
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

/// <summary>Windows has no input-permission concept; injection is always permitted.</summary>
public sealed class WindowsPermissionGate : IPermissionGate
{
    public bool CanInjectInput => true;
}
```

- [ ] **Step 2: Create the one-time-prompt abstraction (spec §6)**

`IPermissionGate` is a pure query; the first-launch system prompt that *registers the app
in the Accessibility pane* is a separate, side-effecting concern. On macOS, without this
call the app never appears in the list and the feature cannot work. Windows has no analog.

`src/KeepAwakeTool.Core/Platform/IInputPermissionPrompt.cs`:

```csharp
namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// One-time, app-startup request to surface the OS input-permission prompt so the app
/// is listed in the relevant settings pane. Windows: no-op. macOS: AXIsProcessTrusted
/// with the prompt option, only when not already trusted.
/// </summary>
public interface IInputPermissionPrompt
{
    void RequestInitialGrantIfNeeded();
}
```

- [ ] **Step 3: Windows no-op implementation**

`src/KeepAwakeTool.Platform.Win/WindowsInputPermissionPrompt.cs`:

```csharp
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsInputPermissionPrompt : IInputPermissionPrompt
{
    public void RequestInitialGrantIfNeeded() { /* no input-permission concept on Windows */ }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Platform.Win/WindowsPermissionGate.cs src/KeepAwakeTool.Core/Platform/IInputPermissionPrompt.cs src/KeepAwakeTool.Platform.Win/WindowsInputPermissionPrompt.cs
git commit -m "$(cat <<'EOF'
feat(core): add IInputPermissionPrompt + WindowsPermissionGate/Prompt (no-ops)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 5: Create `Platform.Mac` stub project, restructure `App.csproj`, update solution

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/KeepAwakeTool.Platform.Mac.csproj`
- Create: `src/KeepAwakeTool.Platform.Mac/Placeholder.cs`
- Modify: `src/KeepAwakeTool.App/KeepAwakeTool.App.csproj`
- Modify: `KeepAwakeTool.slnx`

- [ ] **Step 1: Create the Platform.Mac project file**

`src/KeepAwakeTool.Platform.Mac/KeepAwakeTool.Platform.Mac.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>KeepAwakeTool.Platform.Mac</RootNamespace>
    <AssemblyName>KeepAwakeTool.Platform.Mac</AssemblyName>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\KeepAwakeTool.Core\KeepAwakeTool.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add a placeholder type so the project produces an assembly**

`src/KeepAwakeTool.Platform.Mac/Placeholder.cs`:

```csharp
namespace KeepAwakeTool.Platform.Mac;

// Replaced by real implementations in Phase 2. Keeps the project non-empty
// so conditional ProjectReferences resolve during Phase 1.
internal static class Placeholder { }
```

- [ ] **Step 3: Restructure the App project file**

Replace the entire contents of `src/KeepAwakeTool.App/KeepAwakeTool.App.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>KeepAwakeTool.App</RootNamespace>
    <AssemblyName>KeepAwakeTool</AssemblyName>
    <UseAvalonia>true</UseAvalonia>
    <_IsWinRid>false</_IsWinRid>
    <_IsOsxRid>false</_IsOsxRid>
    <_IsWinRid Condition="$(RuntimeIdentifier.StartsWith('win'))">true</_IsWinRid>
    <_IsOsxRid Condition="$(RuntimeIdentifier.StartsWith('osx'))">true</_IsOsxRid>
    <_NoRid Condition="'$(RuntimeIdentifier)' == ''">true</_NoRid>
  </PropertyGroup>

  <!-- Windows publish keeps the application manifest; never applied on macOS. -->
  <ItemGroup Condition="'$(_IsWinRid)' == 'true' or '$(_NoRid)' == 'true'">
    <ApplicationManifest Include="app.manifest" />
  </ItemGroup>

  <ItemGroup>
    <AvaloniaResource Include="Tray/Assets/*.ico" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\KeepAwakeTool.Core\KeepAwakeTool.Core.csproj" />
    <PackageReference Include="Avalonia" Version="12.0.3" />
    <PackageReference Include="Avalonia.Desktop" Version="12.0.3" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.3" />
    <PackageReference Include="Avalonia.Controls.ItemsRepeater" Version="12.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.8" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.8" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.8" />
  </ItemGroup>

  <!-- Platform projects: referenced for matching RID, or both when no RID (build/test). -->
  <ItemGroup Condition="'$(_IsWinRid)' == 'true' or '$(_NoRid)' == 'true'">
    <ProjectReference Include="..\KeepAwakeTool.Platform.Win\KeepAwakeTool.Platform.Win.csproj" />
  </ItemGroup>
  <ItemGroup Condition="'$(_IsOsxRid)' == 'true' or '$(_NoRid)' == 'true'">
    <ProjectReference Include="..\KeepAwakeTool.Platform.Mac\KeepAwakeTool.Platform.Mac.csproj" />
  </ItemGroup>
</Project>
```

Note: `Microsoft.Win32.SystemEvents` is intentionally removed (moved to Platform.Win in Task 2). `app.manifest` stays on disk.

- [ ] **Step 4: Add Platform.Mac to the solution**

Replace `KeepAwakeTool.slnx` contents with:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/KeepAwakeTool.App/KeepAwakeTool.App.csproj" />
    <Project Path="src/KeepAwakeTool.Core/KeepAwakeTool.Core.csproj" />
    <Project Path="src/KeepAwakeTool.Platform.Win/KeepAwakeTool.Platform.Win.csproj" />
    <Project Path="src/KeepAwakeTool.Platform.Mac/KeepAwakeTool.Platform.Mac.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/KeepAwakeTool.Core.Tests/KeepAwakeTool.Core.Tests.csproj" />
    <Project Path="tests/KeepAwakeTool.App.Tests/KeepAwakeTool.App.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 5: Verify the solution builds (App now has both platform refs; ServiceRegistration still compiles because Platform.Win types exist)**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors. (ServiceRegistration still references only Platform.Win types — that is fixed in Task 6. The App TFM is now `net10.0`.)

- [ ] **Step 6: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/ src/KeepAwakeTool.App/KeepAwakeTool.App.csproj KeepAwakeTool.slnx
git commit -m "$(cat <<'EOF'
refactor(app): neutral net10.0 TFM + conditional platform ProjectReferences

App is now plain net10.0/Exe; Platform.Win referenced for win RID or no RID,
Platform.Mac for osx RID or no RID. app.manifest applied on Windows only.
Adds the Platform.Mac project (stub; implemented in Phase 2).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 6: Rewire `ServiceRegistration`, `App.axaml.cs`, `Program.cs`; delete old guard

**Files:**
- Modify: `src/KeepAwakeTool.App/Composition/ServiceRegistration.cs`
- Modify: `src/KeepAwakeTool.App/App.axaml.cs`
- Modify: `src/KeepAwakeTool.App/Program.cs`
- Delete: `src/KeepAwakeTool.App/SingleInstance/SingleInstanceGuard.cs`

- [ ] **Step 1: Rewrite ServiceRegistration with an OS switch**

Replace `src/KeepAwakeTool.App/Composition/ServiceRegistration.cs` lines 42–55 (the `if (!RuntimeInformation.IsOSPlatform(...)) throw;` block through the `IGlobalHotkeyService` registration) with:

```csharp
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!;
            services.AddSingleton<IInputSimulator, WindowsInputSimulator>();
            services.AddSingleton<IIdleMonitor, WindowsIdleMonitor>();
            services.AddSingleton<IPowerManager, WindowsPowerManager>();
            services.AddSingleton<IAutoStartManager>(_ => new WindowsAutoStartManager(exePath));
            services.AddSingleton<ISessionInfo, WindowsSessionInfo>();
            services.AddSingleton<IPermissionGate, WindowsPermissionGate>();
            services.AddSingleton<IInputPermissionPrompt, WindowsInputPermissionPrompt>();
            services.AddSingleton<ISystemPowerEvents, WindowsSystemPowerEvents>();
            services.AddSingleton<IGlobalHotkeyService>(sp =>
            {
                var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
                return new WindowsGlobalHotkeyService((level, msg) => fl.Log(level, msg));
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS implementations are registered here in Phase 2 (Task 17).
            throw new PlatformNotSupportedException("macOS platform services land in Phase 2.");
        }
        else
        {
            throw new PlatformNotSupportedException("Supported platforms: Windows, macOS.");
        }
```

Then update the `ActivityEngine` registration (was lines 57–62) to pass the gate:

```csharp
        services.AddSingleton<ActivityEngine>(sp => new ActivityEngine(
            sp.GetRequiredService<IInputSimulator>(),
            sp.GetRequiredService<IIdleMonitor>(),
            sp.GetRequiredService<IPowerManager>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Func<AppConfig>>()(),
            sp.GetRequiredService<IPermissionGate>()));
```

Keep the existing `using KeepAwakeTool.Platform.Win;` (only reachable on Windows at runtime; the type references resolve at compile time because Platform.Win is referenced in the no-RID build).

- [ ] **Step 2: Replace direct SystemEvents use in App.axaml.cs + add the one-time permission prompt**

In `src/KeepAwakeTool.App/App.axaml.cs`:

Immediately after the `_pump.Start();` line and its `_log.Log("INFO", "Engine pump started");`
line, add the one-time input-permission prompt (no-op on Windows; macOS surfaces the
Accessibility prompt only when not yet trusted — spec §6):

```csharp
            Services.GetRequiredService<KeepAwakeTool.Core.Platform.IInputPermissionPrompt>()
                .RequestInitialGrantIfNeeded();
```

Replace line 65 (`Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;`) with:

```csharp
            var powerEvents = Services.GetRequiredService<KeepAwakeTool.Core.Platform.ISystemPowerEvents>();
            powerEvents.Resumed += OnSystemResumed;
            powerEvents.Start();
```

Replace the `desktop.Exit += (_, _) => { ... }` body (lines 91–98) with:

```csharp
            desktop.Exit += (_, _) =>
            {
                _log?.Log("INFO", "Shutting down");
                powerEvents.Resumed -= OnSystemResumed;
                powerEvents.Stop();
                _pump?.Dispose();
                power.Stop();
                (Services.GetRequiredService<IGlobalHotkeyService>() as IDisposable)?.Dispose();
            };
```

Replace the `OnPowerModeChanged` method (lines 150–159) with:

```csharp
    private void OnSystemResumed()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Services.GetRequiredService<Core.Power.PowerModeController>().Rearm();
        });
    }
```

- [ ] **Step 3: Replace SingleInstanceGuard with the abstraction in Program.cs**

Replace the entire contents of `src/KeepAwakeTool.App/Program.cs` with:

```csharp
using Avalonia;
using KeepAwakeTool.Core.Platform;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KeepAwakeTool.App;

internal static class Program
{
    private static ISingleInstanceGuard? _guard;

    [STAThread]
    public static int Main(string[] args)
    {
        var logger = new KeepAwakeTool.Core.Diagnostics.FileLogger(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeepAwakeTool", "logs"));

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Log("FATAL", e.ExceptionObject?.ToString() ?? "unknown");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Log("ERROR", e.Exception.ToString());
            e.SetObserved();
        };

        _guard = CreateGuard();
        if (!_guard.TryAcquire())
        {
            _guard.SignalExistingInstance();
            return 0;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        finally
        {
            _guard.Release();
        }
    }

    private static ISingleInstanceGuard CreateGuard()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new KeepAwakeTool.Platform.Win.WindowsSingleInstanceGuard();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("macOS single-instance guard lands in Phase 2.");
        throw new PlatformNotSupportedException("Supported platforms: Windows, macOS.");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
```

- [ ] **Step 4: Delete the obsolete guard**

```bash
git rm src/KeepAwakeTool.App/SingleInstance/SingleInstanceGuard.cs
```

- [ ] **Step 5: Build and run the full suite**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

Run: `dotnet test -c Release -- xUnit.ParallelizeTestCollections=false`
Expected: 53 passing (51 original + 2 from Task 1). App.Tests still green (no behavior change on the tested Windows path; macOS branch throws only at runtime, not under test).

- [ ] **Step 6: Commit**

```bash
git add src/KeepAwakeTool.App/Composition/ServiceRegistration.cs src/KeepAwakeTool.App/App.axaml.cs src/KeepAwakeTool.App/Program.cs
git commit -m "$(cat <<'EOF'
refactor(app): OS-switched DI + ISystemPowerEvents/ISingleInstanceGuard wiring

ServiceRegistration selects Windows impls under an IsOSPlatform switch (macOS
branch throws until Phase 2). App.axaml.cs consumes ISystemPowerEvents instead
of Microsoft.Win32.SystemEvents. Program.cs uses ISingleInstanceGuard. Old
SingleInstanceGuard removed (logic moved to Platform.Win in Task 3).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 7: Verify Windows publish output is unaffected

**Files:** none (verification only).

- [ ] **Step 1: Capture the macOS no-RID build state**

Run: `dotnet build -c Release` and `dotnet test -c Release -- xUnit.ParallelizeTestCollections=false`
Expected: Build succeeded; 53 tests pass.

- [ ] **Step 2: Cross-verify the Windows publish graph still resolves only Platform.Win**

Run: `dotnet publish src/KeepAwakeTool.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o /tmp/kat-winpub 2>&1 | tail -5`
Expected: "Build succeeded" / publish completes. (Cross-publish to win-x64 from macOS validates the conditional refs: the `osx` ItemGroup is excluded, only Platform.Win is referenced. This is a graph/compile check, not a runtime check.)

- [ ] **Step 3: Confirm Platform.Mac is NOT in the Windows publish output**

Run: `ls /tmp/kat-winpub | grep -i 'KeepAwakeTool.Platform' || true`
Expected: `KeepAwakeTool.Platform.Win.dll` present (or folded into the single file); **no** `KeepAwakeTool.Platform.Mac.dll`. If single-file hides it, instead run:
`dotnet build src/KeepAwakeTool.App -c Release -r win-x64 -o /tmp/kat-winbuild && ls /tmp/kat-winbuild | grep Platform`
Expected: only `KeepAwakeTool.Platform.Win.dll`.

- [ ] **Step 4: Clean up**

```bash
rm -rf /tmp/kat-winpub /tmp/kat-winbuild
```

No commit (verification task). If Step 3 shows `Platform.Mac.dll` in the Windows output, the conditional `ProjectReference` is wrong — fix the `Condition` expressions in `KeepAwakeTool.App.csproj` (Task 5 Step 3) before proceeding.

---

# Phase 2 — KeepAwakeTool.Platform.Mac

Goal: macOS implementations of all six platform interfaces + `IPermissionGate`/`ISystemPowerEvents`/`ISingleInstanceGuard`, wired into DI. P/Invoke correctness is validated by macOS-only integration tests (`[Trait("Category","PlatformIntegration")]`, excluded from default `dotnet test`) and the manual smoke (spec §11) — pure interop is not unit-testable.

Delete `src/KeepAwakeTool.Platform.Mac/Placeholder.cs` once the first real type is added (Task 8 Step 4).

## Task 8: macOS P/Invoke interop bindings

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/Interop/CoreFoundation.cs`
- Create: `src/KeepAwakeTool.Platform.Mac/Interop/CoreGraphics.cs`
- Create: `src/KeepAwakeTool.Platform.Mac/Interop/IOKit.cs`
- Create: `src/KeepAwakeTool.Platform.Mac/Interop/Carbon.cs`
- Create: `src/KeepAwakeTool.Platform.Mac/Interop/ObjC.cs`
- Delete: `src/KeepAwakeTool.Platform.Mac/Placeholder.cs`

- [ ] **Step 1: CoreFoundation bindings**

`src/KeepAwakeTool.Platform.Mac/Interop/CoreFoundation.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class CoreFoundation
{
    private const string Lib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(Lib)]
    internal static extern void CFRelease(IntPtr cf);

    [DllImport(Lib)]
    internal static extern IntPtr CFStringCreateWithCString(IntPtr alloc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr, uint encoding);

    internal const uint kCFStringEncodingUTF8 = 0x08000100;

    [DllImport(Lib)]
    internal static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(Lib)]
    internal static extern void CFRunLoopRun();

    [DllImport(Lib)]
    internal static extern void CFRunLoopStop(IntPtr rl);

    [DllImport(Lib)]
    internal static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    // kCFRunLoopCommonModes is an exported CFStringRef data symbol (read once).
    private static readonly Lazy<IntPtr> _commonModes = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(Lib), "kCFRunLoopCommonModes")));
    internal static IntPtr kCFRunLoopCommonModes => _commonModes.Value;

    internal static IntPtr CFStr(string s) =>
        CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8);
}
```

- [ ] **Step 2: CoreGraphics bindings (input + idle)**

`src/KeepAwakeTool.Platform.Mac/Interop/CoreGraphics.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class CoreGraphics
{
    private const string Lib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    // CGEventType
    internal const uint kCGEventMouseMoved = 5;
    // CGEventTapLocation
    internal const uint kCGHIDEventTap = 0;
    // CGEventSourceStateID
    internal const int kCGEventSourceStateHIDSystemState = 1;
    // CGEventType used by CGEventSourceSecondsSinceLastEventType for "any input"
    internal const uint kCGAnyInputEventType = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGPoint { public double X; public double Y; public CGPoint(double x, double y){X=x;Y=y;} }

    [DllImport(Lib)]
    internal static extern double CGEventSourceSecondsSinceLastEventType(int stateID, uint eventType);

    [DllImport(Lib)]
    internal static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [DllImport(Lib)]
    internal static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [DllImport(Lib)]
    internal static extern void CGEventPost(uint tap, IntPtr @event);

    [DllImport(Lib)]
    internal static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(Lib)]
    internal static extern CGPoint CGEventGetLocation(IntPtr @event);
}
```

- [ ] **Step 3: IOKit bindings (power assertion + system-power notifications)**

`src/KeepAwakeTool.Platform.Mac/Interop/IOKit.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class IOKit
{
    private const string Lib = "/System/Library/Frameworks/IOKit.framework/IOKit";

    internal const uint kIOPMAssertionLevelOn = 255;
    // Message type delivered to the IOServiceInterestCallback on wake.
    internal const uint kIOMessageSystemHasPoweredOn = 0xE0000300;

    [DllImport(Lib)]
    internal static extern int IOPMAssertionCreateWithName(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string assertionType, uint assertionLevel,
        IntPtr assertionName, out uint assertionID);

    [DllImport(Lib)]
    internal static extern int IOPMAssertionRelease(uint assertionID);

    // Power source / sleep notifications. Kept as a delegate; callers GC-root
    // the instance and pass Marshal.GetFunctionPointerForDelegate(...) as IntPtr.
    internal delegate void IOServiceInterestCallback(IntPtr refcon, IntPtr service, uint messageType, IntPtr messageArgument);

    [DllImport(Lib)]
    internal static extern IntPtr IORegisterForSystemPower(IntPtr refcon, out IntPtr thePortRef,
        IntPtr callback, out IntPtr notifier);

    [DllImport(Lib)]
    internal static extern IntPtr IONotificationPortGetRunLoopSource(IntPtr notify);

    [DllImport(Lib)]
    internal static extern int IODeregisterForSystemPower(ref IntPtr notifier);

    [DllImport(Lib)]
    internal static extern void IOAllowPowerChange(IntPtr kernPort, IntPtr notificationID);

    internal const string kIOPMAssertPreventUserIdleSystemSleep = "PreventUserIdleSystemSleep";
}
```

- [ ] **Step 4: Carbon bindings (global hotkey) and delete the placeholder**

`src/KeepAwakeTool.Platform.Mac/Interop/Carbon.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class Carbon
{
    private const string Lib = "/System/Library/Frameworks/Carbon.framework/Carbon";

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventTypeSpec { public uint eventClass; public uint eventKind; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventHotKeyID { public uint signature; public uint id; }

    internal const uint kEventClassKeyboard = 0x6B657962;   // 'keyb'
    internal const uint kEventHotKeyPressed = 5;
    internal const uint typeEventHotKeyID = 0x686B6964;     // 'hkid'
    internal const uint kEventParamDirectObject = 0x2D2D2D2D; // '----'

    // Carbon modifier masks
    internal const uint cmdKey = 0x0100;
    internal const uint shiftKey = 0x0200;
    internal const uint optionKey = 0x0800;
    internal const uint controlKey = 0x1000;

    internal delegate int EventHandlerProcPtr(IntPtr inHandlerCallRef, IntPtr inEvent, IntPtr inUserData);

    [DllImport(Lib)]
    internal static extern int RegisterEventHotKey(uint inHotKeyCode, uint inHotKeyModifiers,
        EventHotKeyID inHotKeyID, IntPtr inTarget, uint inOptions, out IntPtr outRef);

    [DllImport(Lib)]
    internal static extern int UnregisterEventHotKey(IntPtr inHotKey);

    [DllImport(Lib)]
    internal static extern IntPtr GetApplicationEventTarget();

    [DllImport(Lib)]
    internal static extern int InstallEventHandler(IntPtr inTarget, EventHandlerProcPtr inHandler,
        int inNumTypes, [In] EventTypeSpec[] inList, IntPtr inUserData, out IntPtr outRef);

    [DllImport(Lib)]
    internal static extern int GetEventParameter(IntPtr inEvent, uint inName, uint inDesiredType,
        IntPtr outActualType, int inBufferSize, IntPtr outActualSize, out EventHotKeyID outData);
}
```

```bash
git rm src/KeepAwakeTool.Platform.Mac/Placeholder.cs
```

- [ ] **Step 5: ObjC runtime bindings (SMAppService + Accessibility)**

`src/KeepAwakeTool.Platform.Mac/Interop/ObjC.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class ObjC
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";

    [DllImport(Lib)]
    internal static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Lib)]
    internal static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Lib)]
    internal static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    // long-returning variant for SMAppServiceStatus
    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern long objc_msgSend_long(IntPtr receiver, IntPtr selector);

    internal static IntPtr Send(IntPtr r, string sel) => objc_msgSend(r, sel_registerName(sel));
    internal static IntPtr SendPtr(IntPtr r, string sel, IntPtr a) => objc_msgSend_ptr(r, sel_registerName(sel), a);
    internal static long SendLong(IntPtr r, string sel) => objc_msgSend_long(r, sel_registerName(sel));
}
```

- [ ] **Step 6: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors. (Interop only; no behavior yet. The macOS DI branch still throws — App.Tests/Core.Tests unaffected.)

- [ ] **Step 7: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/Interop/
git commit -m "$(cat <<'EOF'
feat(platform-mac): add CoreFoundation/CoreGraphics/IOKit/Carbon/ObjC bindings

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 9: `MacIdleMonitor`

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacIdleMonitor.cs`

- [ ] **Step 1: Implement**

`src/KeepAwakeTool.Platform.Mac/MacIdleMonitor.cs`:

```csharp
using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacIdleMonitor : IIdleMonitor
{
    public TimeSpan TimeSinceLastUserInput()
    {
        var seconds = CoreGraphics.CGEventSourceSecondsSinceLastEventType(
            CoreGraphics.kCGEventSourceStateHIDSystemState,
            CoreGraphics.kCGAnyInputEventType);
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        return TimeSpan.FromSeconds(seconds);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacIdleMonitor.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacIdleMonitor via CGEventSourceSecondsSinceLastEventType

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 10: `MacInputSimulator`

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacInputSimulator.cs`

- [ ] **Step 1: Implement (mirrors WindowsInputSimulator's no-drift jiggle: single alternating ±N hop)**

`src/KeepAwakeTool.Platform.Mac/MacInputSimulator.cs`:

```csharp
using System;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacInputSimulator : IInputSimulator
{
    private int _jiggleSign = 1;

    public void MoveMouse(MouseMode mode, int jigglePixels)
    {
        // Current cursor position via a throwaway event.
        var probe = CoreGraphics.CGEventCreate(IntPtr.Zero);
        var pos = CoreGraphics.CGEventGetLocation(probe);
        if (probe != IntPtr.Zero) CoreFoundation.CFRelease(probe);

        double dx = 0;
        if (mode == MouseMode.Jiggle)
        {
            dx = _jiggleSign * jigglePixels;
            _jiggleSign = -_jiggleSign;
        }

        var target = new CoreGraphics.CGPoint(pos.X + dx, pos.Y);
        var move = CoreGraphics.CGEventCreateMouseEvent(IntPtr.Zero,
            CoreGraphics.kCGEventMouseMoved, target, 0);
        if (move == IntPtr.Zero) return;
        CoreGraphics.CGEventPost(CoreGraphics.kCGHIDEventTap, move);
        CoreFoundation.CFRelease(move);
    }

    public void SendKey(VirtualKey key)
    {
        ushort code = key switch
        {
            VirtualKey.F13 => 105,
            VirtualKey.F14 => 107,
            VirtualKey.F15 => 113,
            _ => 113
        };
        var down = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, code, true);
        if (down != IntPtr.Zero)
        {
            CoreGraphics.CGEventPost(CoreGraphics.kCGHIDEventTap, down);
            CoreFoundation.CFRelease(down);
        }
        var up = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, code, false);
        if (up != IntPtr.Zero)
        {
            CoreGraphics.CGEventPost(CoreGraphics.kCGHIDEventTap, up);
            CoreFoundation.CFRelease(up);
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacInputSimulator.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacInputSimulator via CGEvent mouse/keyboard injection

Mirrors the Windows no-drift jiggle (single alternating +/-N hop). F13/F14/F15
map to macOS virtual keycodes 105/107/113.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 11: `MacPowerManager`

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacPowerManager.cs`

- [ ] **Step 1: Implement (idempotent assertion + pmset display-off, failures logged not thrown)**

`src/KeepAwakeTool.Platform.Mac/MacPowerManager.cs`:

```csharp
using System;
using System.Diagnostics;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacPowerManager : IPowerManager
{
    private readonly Action<string, string>? _log;
    private uint _assertionId;
    private bool _asserted;

    public MacPowerManager(Action<string, string>? log = null) => _log = log;

    public void KeepSystemAwake(bool on)
    {
        if (on)
        {
            if (_asserted) return;
            var name = CoreFoundation.CFStr("KeepAwakeTool");
            try
            {
                var rc = IOKit.IOPMAssertionCreateWithName(
                    IOKit.kIOPMAssertPreventUserIdleSystemSleep,
                    IOKit.kIOPMAssertionLevelOn, name, out _assertionId);
                if (rc == 0) _asserted = true;
                else _log?.Invoke("ERROR", $"IOPMAssertionCreateWithName failed rc={rc}");
            }
            finally { if (name != IntPtr.Zero) CoreFoundation.CFRelease(name); }
        }
        else
        {
            if (!_asserted) return;
            var rc = IOKit.IOPMAssertionRelease(_assertionId);
            if (rc != 0) _log?.Invoke("ERROR", $"IOPMAssertionRelease failed rc={rc}");
            _asserted = false;
            _assertionId = 0;
        }
    }

    public void ForceDisplayOff()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("/usr/bin/pmset", "displaysleepnow")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            _log?.Invoke("ERROR", "pmset displaysleepnow failed: " + ex.Message);
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacPowerManager.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacPowerManager via IOPMAssertion + pmset displaysleepnow

Idempotent awake assertion; display-off failures are logged, never thrown.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 12: `MacAccessibility` + `MacPermissionGate`

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacAccessibility.cs`
- Create: `src/KeepAwakeTool.Platform.Mac/MacPermissionGate.cs`
- Create: `src/KeepAwakeTool.Platform.Mac/MacInputPermissionPrompt.cs`

- [ ] **Step 1: Implement the Accessibility probe**

`src/KeepAwakeTool.Platform.Mac/MacAccessibility.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public static class MacAccessibility
{
    private const string AppServices =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CF =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(AppServices)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    [DllImport(CF)]
    private static extern IntPtr CFDictionaryCreate(IntPtr alloc, IntPtr[] keys, IntPtr[] values,
        long numValues, IntPtr keyCallBacks, IntPtr valueCallBacks);

    // Exported CFStringRef / CFBooleanRef data symbols (read once each).
    private static readonly Lazy<IntPtr> _promptKey = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(
            NativeLibrary.Load(AppServices), "kAXTrustedCheckOptionPrompt")));
    private static readonly Lazy<IntPtr> _cfTrue = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(CF), "kCFBooleanTrue")));

    /// <summary>Checks Accessibility trust WITHOUT prompting.</summary>
    public static bool IsTrusted() => AXIsProcessTrustedWithOptions(IntPtr.Zero);

    /// <summary>Checks trust and shows the system prompt if not yet trusted.</summary>
    public static bool RequestTrust()
    {
        var keys = new[] { _promptKey.Value };
        var vals = new[] { _cfTrue.Value };
        var dict = CFDictionaryCreate(IntPtr.Zero, keys, vals, 1, IntPtr.Zero, IntPtr.Zero);
        try { return AXIsProcessTrustedWithOptions(dict); }
        finally { if (dict != IntPtr.Zero) CoreFoundation.CFRelease(dict); }
    }
}
```

- [ ] **Step 2: Implement the gate**

`src/KeepAwakeTool.Platform.Mac/MacPermissionGate.cs`:

```csharp
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>Re-evaluates Accessibility trust on every access (no caching) so a
/// runtime grant is picked up on the next pump tick without a restart.</summary>
public sealed class MacPermissionGate : IPermissionGate
{
    public bool CanInjectInput => MacAccessibility.IsTrusted();
}
```

- [ ] **Step 3: Implement the one-time prompt**

`src/KeepAwakeTool.Platform.Mac/MacInputPermissionPrompt.cs`:

```csharp
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacInputPermissionPrompt : IInputPermissionPrompt
{
    public void RequestInitialGrantIfNeeded()
    {
        // Only prompt when not already trusted; RequestTrust shows the system
        // dialog and registers the app in Privacy ▸ Accessibility (toggle off).
        if (!MacAccessibility.IsTrusted())
            MacAccessibility.RequestTrust();
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacAccessibility.cs src/KeepAwakeTool.Platform.Mac/MacPermissionGate.cs src/KeepAwakeTool.Platform.Mac/MacInputPermissionPrompt.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacAccessibility probe + MacPermissionGate + one-time prompt

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 13: `MacAutoStartManager` (SMAppService, macOS 14+)

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacAutoStartManager.cs`

- [ ] **Step 1: Implement via the ObjC runtime**

`src/KeepAwakeTool.Platform.Mac/MacAutoStartManager.cs`:

```csharp
using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>
/// Login-item registration via SMAppService.mainApp (macOS 13+; baseline here is 14+).
/// SMAppServiceStatus: 0 = NotRegistered, 1 = Enabled, 2 = RequiresApproval, 3 = NotFound.
/// </summary>
public sealed class MacAutoStartManager : IAutoStartManager
{
    private readonly Action<string, string>? _log;
    public MacAutoStartManager(Action<string, string>? log = null) => _log = log;

    private static IntPtr MainAppService()
    {
        var cls = ObjC.objc_getClass("SMAppService");
        return cls == IntPtr.Zero ? IntPtr.Zero : ObjC.Send(cls, "mainApp");
    }

    public bool IsEnabled
    {
        get
        {
            var svc = MainAppService();
            if (svc == IntPtr.Zero) return false;
            return ObjC.SendLong(svc, "status") == 1; // SMAppServiceStatusEnabled
        }
    }

    public void Enable()
    {
        var svc = MainAppService();
        if (svc == IntPtr.Zero) { _log?.Invoke("ERROR", "SMAppService unavailable"); return; }
        ObjC.SendPtr(svc, "registerAndReturnError:", IntPtr.Zero);
    }

    public void Disable()
    {
        var svc = MainAppService();
        if (svc == IntPtr.Zero) { _log?.Invoke("ERROR", "SMAppService unavailable"); return; }
        ObjC.SendPtr(svc, "unregisterAndReturnError:", IntPtr.Zero);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacAutoStartManager.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacAutoStartManager via SMAppService.mainApp

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 14: `MacSessionInfo` (stub)

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacSessionInfo.cs`

- [ ] **Step 1: Implement the documented stub (spec §2 / §14: only consumer is the RDP tooltip note)**

`src/KeepAwakeTool.Platform.Mac/MacSessionInfo.cs`:

```csharp
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>Screen-sharing/remote detection is explicitly out of scope (spec §2).
/// The sole consumer is the RDP tooltip note; returning false is correct v2 behavior.</summary>
public sealed class MacSessionInfo : ISessionInfo
{
    public bool IsRemoteSession => false;
}
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacSessionInfo.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacSessionInfo stub (remote detection out of scope)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 15: `RunLoopThread` helper + `MacSystemPowerEvents`

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/RunLoopThread.cs`
- Create: `src/KeepAwakeTool.Platform.Mac/MacSystemPowerEvents.cs`

- [ ] **Step 1: Implement a reusable CFRunLoop background thread**

`src/KeepAwakeTool.Platform.Mac/RunLoopThread.cs`:

```csharp
using System;
using System.Threading;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>
/// Owns a dedicated background thread running a CFRunLoop. Mirrors the Windows
/// hidden-message-pump design (one persistent thread; work marshaled onto it).
/// </summary>
internal sealed class RunLoopThread : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private IntPtr _runLoop;

    public RunLoopThread(string name, Action onRunLoopStarted)
    {
        _thread = new Thread(() =>
        {
            _runLoop = CoreFoundation.CFRunLoopGetCurrent();
            onRunLoopStarted();
            _ready.Set();
            CoreFoundation.CFRunLoopRun();
        }) { IsBackground = true, Name = name };
    }

    public IntPtr RunLoop => _runLoop;
    public void Start() { _thread.Start(); _ready.Wait(); }

    public void Dispose()
    {
        if (_runLoop != IntPtr.Zero) CoreFoundation.CFRunLoopStop(_runLoop);
        if (_thread.IsAlive) _thread.Join(2000);
        _ready.Dispose();
    }
}
```

- [ ] **Step 2: Implement MacSystemPowerEvents**

`src/KeepAwakeTool.Platform.Mac/MacSystemPowerEvents.cs`:

```csharp
using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacSystemPowerEvents : ISystemPowerEvents
{
    public event Action? Resumed;

    private RunLoopThread? _loop;
    private IntPtr _rootPort;
    private IntPtr _notifier;
    private IntPtr _notifyPort;
    private IOKit.IOServiceInterestCallback? _callback; // kept alive against GC

    public void Start()
    {
        if (_loop is not null) return;
        _callback = OnPower;
        var cbPtr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_callback);

        _loop = new RunLoopThread("KAT-PowerEvents", () =>
        {
            _rootPort = IOKit.IORegisterForSystemPower(IntPtr.Zero, out _notifyPort, cbPtr, out _notifier);
            if (_notifyPort != IntPtr.Zero)
            {
                var src = IOKit.IONotificationPortGetRunLoopSource(_notifyPort);
                CoreFoundation.CFRunLoopAddSource(
                    CoreFoundation.CFRunLoopGetCurrent(), src, CoreFoundation.kCFRunLoopCommonModes);
            }
        });
        _loop.Start();
    }

    private void OnPower(IntPtr refcon, IntPtr service, uint messageType, IntPtr messageArgument)
    {
        // Acknowledge sleep/wake messages so the system is not blocked.
        IOKit.IOAllowPowerChange(_rootPort, messageArgument);
        if (messageType == IOKit.kIOMessageSystemHasPoweredOn) Resumed?.Invoke();
    }

    public void Stop()
    {
        if (_loop is null) return;
        if (_notifier != IntPtr.Zero) IOKit.IODeregisterForSystemPower(ref _notifier);
        _loop.Dispose();
        _loop = null;
        _callback = null;
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 3: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/RunLoopThread.cs src/KeepAwakeTool.Platform.Mac/MacSystemPowerEvents.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): RunLoopThread helper + MacSystemPowerEvents (IOKit)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 16: `MacSingleInstanceGuard`

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacSingleInstanceGuard.cs`

- [ ] **Step 1: Implement an exclusive-lock-file guard (spec §3.3 / §9.4)**

`src/KeepAwakeTool.Platform.Mac/MacSingleInstanceGuard.cs`:

```csharp
using System;
using System.IO;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacSingleInstanceGuard : ISingleInstanceGuard
{
    private FileStream? _lock;

    private static string LockPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeepAwakeTool");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "instance.lock");
    }

    public bool TryAcquire()
    {
        try
        {
            // FileShare.None gives an exclusive OS-level lock; a second process
            // opening the same path fails until this stream is closed/process dies.
            _lock = new FileStream(LockPath(), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void SignalExistingInstance()
    {
        // v1 parity: the running tray icon is the "already running" signal.
    }

    public void Release()
    {
        _lock?.Dispose();
        _lock = null;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacSingleInstanceGuard.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacSingleInstanceGuard via exclusive lock file

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 17: `MacGlobalHotkeyService`

**Files:**
- Create: `src/KeepAwakeTool.Platform.Mac/MacGlobalHotkeyService.cs`

- [ ] **Step 1: Implement (Carbon RegisterEventHotKey on the run-loop thread; async RegistrationResult)**

`src/KeepAwakeTool.Platform.Mac/MacGlobalHotkeyService.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const uint Signature = 0x4B415448; // 'KATH'
    private const uint HotkeyId = 1;

    private readonly Action<string, string>? _log;
    private RunLoopThread? _loop;
    private Action? _callback;
    private IntPtr _hotKeyRef;
    private Carbon.EventHandlerProcPtr? _handler;   // GC-rooted
    private Hotkey? _pending;

    public event Action<bool>? RegistrationResult;

    public MacGlobalHotkeyService(Action<string, string>? log = null) => _log = log;

    public bool TryRegister(Hotkey hotkey, Action onPressed)
    {
        _callback = onPressed;
        _pending = hotkey;

        if (_loop is null)
        {
            _handler = HandleHotKey;
            _loop = new RunLoopThread("KAT-HotkeyPump", () =>
            {
                var spec = new Carbon.EventTypeSpec
                {
                    eventClass = Carbon.kEventClassKeyboard,
                    eventKind = Carbon.kEventHotKeyPressed
                };
                Carbon.InstallEventHandler(Carbon.GetApplicationEventTarget(), _handler!,
                    1, new[] { spec }, IntPtr.Zero, out _);
                DoRegister();
            });
            _loop.Start();
        }
        else
        {
            DoRegister();
        }
        return true; // real result arrives async via RegistrationResult
    }

    private void DoRegister()
    {
        if (_hotKeyRef != IntPtr.Zero)
        {
            Carbon.UnregisterEventHotKey(_hotKeyRef);
            _hotKeyRef = IntPtr.Zero;
        }
        if (_pending is null) { RegistrationResult?.Invoke(false); return; }

        var code = MapKey(_pending.Key);
        if (code == 0xFFFF)
        {
            _log?.Invoke("ERROR", $"Hotkey key not mappable: '{_pending.Key}'");
            RegistrationResult?.Invoke(false);
            return;
        }

        uint mods =
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Ctrl)  ? Carbon.controlKey : 0u) |
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Alt)   ? Carbon.optionKey  : 0u) |
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Shift) ? Carbon.shiftKey   : 0u) |
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Win)   ? Carbon.cmdKey     : 0u);

        var id = new Carbon.EventHotKeyID { signature = Signature, id = HotkeyId };
        var rc = Carbon.RegisterEventHotKey(code, mods, id, Carbon.GetApplicationEventTarget(),
            0, out _hotKeyRef);
        var ok = rc == 0 && _hotKeyRef != IntPtr.Zero;
        _log?.Invoke(ok ? "INFO" : "ERROR", $"RegisterEventHotKey rc={rc}");
        RegistrationResult?.Invoke(ok);
    }

    private int HandleHotKey(IntPtr callRef, IntPtr evt, IntPtr userData)
    {
        var rc = Carbon.GetEventParameter(evt, Carbon.kEventParamDirectObject,
            Carbon.typeEventHotKeyID, IntPtr.Zero, Marshal.SizeOf<Carbon.EventHotKeyID>(),
            IntPtr.Zero, out var hkId);
        if (rc == 0 && hkId.signature == Signature && hkId.id == HotkeyId)
            _callback?.Invoke();
        return 0; // noErr
    }

    public void Unregister()
    {
        if (_hotKeyRef != IntPtr.Zero)
        {
            Carbon.UnregisterEventHotKey(_hotKeyRef);
            _hotKeyRef = IntPtr.Zero;
        }
        _callback = null;
        _pending = null;
    }

    public void Dispose()
    {
        Unregister();
        _loop?.Dispose();
        _loop = null;
        _handler = null;
    }

    // macOS virtual keycodes for the keys the Record UI can capture.
    private static ushort MapKey(string key) => key.ToUpperInvariant() switch
    {
        "A" => 0,  "S" => 1,  "D" => 2,  "F" => 3,  "H" => 4,  "G" => 5,
        "Z" => 6,  "X" => 7,  "C" => 8,  "V" => 9,  "B" => 11, "Q" => 12,
        "W" => 13, "E" => 14, "R" => 15, "Y" => 16, "T" => 17,
        "1" => 18, "2" => 19, "3" => 20, "4" => 21, "6" => 22, "5" => 23,
        "9" => 25, "7" => 26, "8" => 28, "0" => 29,
        "O" => 31, "U" => 32, "I" => 34, "P" => 35, "L" => 37, "J" => 38,
        "K" => 40, "N" => 45, "M" => 46,
        "F1" => 122, "F2" => 120, "F3" => 99,  "F4" => 118,
        "F5" => 96,  "F6" => 97,  "F7" => 98,  "F8" => 100,
        "F9" => 101, "F10" => 109, "F11" => 103, "F12" => 111,
        "F13" => 105, "F14" => 107, "F15" => 113,
        _ => 0xFFFF
    };
}
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.Platform.Mac/MacGlobalHotkeyService.cs
git commit -m "$(cat <<'EOF'
feat(platform-mac): MacGlobalHotkeyService via Carbon RegisterEventHotKey

Run-loop pump thread mirrors the Windows hidden-message-pump; real
register success/failure is surfaced async via RegistrationResult.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 18: Wire the macOS DI branch + Program.cs guard + integration tests project

**Files:**
- Modify: `src/KeepAwakeTool.App/Composition/ServiceRegistration.cs`
- Modify: `src/KeepAwakeTool.App/Program.cs`
- Create: `tests/KeepAwakeTool.Platform.Mac.Tests/KeepAwakeTool.Platform.Mac.Tests.csproj`
- Create: `tests/KeepAwakeTool.Platform.Mac.Tests/MacPlatformIntegrationTests.cs`
- Modify: `KeepAwakeTool.slnx`

- [ ] **Step 1: Replace the throwing macOS branch in ServiceRegistration**

In `src/KeepAwakeTool.App/Composition/ServiceRegistration.cs`, add `using KeepAwakeTool.Platform.Mac;` at the top (next to the existing `using KeepAwakeTool.Platform.Win;`). Replace the `else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { throw ... }` block (added in Task 6 Step 1) with:

```csharp
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IInputSimulator, MacInputSimulator>();
            services.AddSingleton<IIdleMonitor, MacIdleMonitor>();
            services.AddSingleton<IPermissionGate, MacPermissionGate>();
            services.AddSingleton<IInputPermissionPrompt, MacInputPermissionPrompt>();
            services.AddSingleton<ISessionInfo, MacSessionInfo>();
            services.AddSingleton<IPowerManager>(sp =>
            {
                var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
                return new MacPowerManager((lvl, msg) => fl.Log(lvl, msg));
            });
            services.AddSingleton<IAutoStartManager>(sp =>
            {
                var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
                return new MacAutoStartManager((lvl, msg) => fl.Log(lvl, msg));
            });
            services.AddSingleton<ISystemPowerEvents, MacSystemPowerEvents>();
            services.AddSingleton<IGlobalHotkeyService>(sp =>
            {
                var fl = sp.GetRequiredService<KeepAwakeTool.Core.Diagnostics.FileLogger>();
                return new MacGlobalHotkeyService((lvl, msg) => fl.Log(lvl, msg));
            });
        }
```

- [ ] **Step 2: Replace the throwing macOS branch in Program.cs**

In `src/KeepAwakeTool.App/Program.cs` `CreateGuard()`, replace the macOS `throw` line with:

```csharp
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new KeepAwakeTool.Platform.Mac.MacSingleInstanceGuard();
```

- [ ] **Step 3: Create the macOS integration test project**

`tests/KeepAwakeTool.Platform.Mac.Tests/KeepAwakeTool.Platform.Mac.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>KeepAwakeTool.Platform.Mac.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\KeepAwakeTool.Platform.Mac\KeepAwakeTool.Platform.Mac.csproj" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
    <PackageReference Include="FluentAssertions" Version="7.2.2" />
  </ItemGroup>
</Project>
```

Note: no `<TargetFramework>`/`<Nullable>` — both are inherited from the root
`Directory.Build.props` (`net10.0`), matching the `KeepAwakeTool.Core.Tests` pattern.
Package versions are copied verbatim from `KeepAwakeTool.Core.Tests.csproj`.

- [ ] **Step 4: Add platform integration tests (gated, macOS-only)**

`tests/KeepAwakeTool.Platform.Mac.Tests/MacPlatformIntegrationTests.cs`:

```csharp
using System;
using FluentAssertions;
using KeepAwakeTool.Platform.Mac;
using Xunit;

namespace KeepAwakeTool.Platform.Mac.Tests;

[Trait("Category", "PlatformIntegration")]
public class MacPlatformIntegrationTests
{
    [Fact]
    public void IdleMonitor_returns_non_negative_timespan()
    {
        var idle = new MacIdleMonitor().TimeSinceLastUserInput();
        idle.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Accessibility_IsTrusted_does_not_throw()
    {
        var act = () => MacAccessibility.IsTrusted();
        act.Should().NotThrow();
    }

    [Fact]
    public void InputSimulator_invisible_move_does_not_throw()
    {
        var act = () => new MacInputSimulator()
            .MoveMouse(KeepAwakeTool.Core.Activity.MouseMode.Invisible, 1);
        act.Should().NotThrow();
    }
}
```

- [ ] **Step 5: Add the test project to the solution**

In `KeepAwakeTool.slnx`, add inside the `/tests/` folder:

```xml
    <Project Path="tests/KeepAwakeTool.Platform.Mac.Tests/KeepAwakeTool.Platform.Mac.Tests.csproj" />
```

- [ ] **Step 6: Build and run the default suite (integration tests excluded)**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

Run: `dotnet test -c Release --filter "Category!=PlatformIntegration&Category!=Timing" -- xUnit.ParallelizeTestCollections=false`
Expected: 53 passing (Core+App). The new `PlatformIntegration`-trait tests are excluded by the filter.

- [ ] **Step 7: Run the macOS integration tests explicitly (this machine is a Mac — per ONBOARDING §3)**

Run: `dotnet test tests/KeepAwakeTool.Platform.Mac.Tests -c Release --filter "Category=PlatformIntegration" -- xUnit.ParallelizeTestCollections=false`
Expected: 3 passing. (`Accessibility_IsTrusted` passes regardless of grant state; `InputSimulator` move is a no-op without Accessibility but must not throw.)

- [ ] **Step 8: Commit**

```bash
git add src/KeepAwakeTool.App/Composition/ServiceRegistration.cs src/KeepAwakeTool.App/Program.cs tests/KeepAwakeTool.Platform.Mac.Tests/ KeepAwakeTool.slnx
git commit -m "$(cat <<'EOF'
feat(app): wire macOS platform implementations into DI + add Mac integration tests

ServiceRegistration and Program.cs now resolve the Platform.Mac impls on macOS.
Adds gated [Category=PlatformIntegration] tests (excluded from default runs).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

# Phase 3 — Accessibility banner UX

Goal: when Accessibility is not granted on macOS, the always-visible window-level banner shows a warning + action buttons, and the tray icon is gray with a tooltip suffix. Windows behavior unchanged.

## Task 19: Settings window banner — warning state + buttons

**Files:**
- Modify: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml`
- Modify: `src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs`

- [ ] **Step 1: Extend the banner Border to host action buttons**

Replace lines 10–12 of `src/KeepAwakeTool.App/Views/SettingsWindow.axaml` (the `<Border DockPanel.Dock="Top" ...>` block) with:

```xml
        <Border DockPanel.Dock="Top" Name="StatusBorder" Background="#22808080" Padding="10,6">
            <Grid ColumnDefinitions="*,Auto,Auto">
                <TextBlock Name="StatusBanner" Grid.Column="0" VerticalAlignment="Center"
                           TextWrapping="Wrap" FontWeight="SemiBold" Text="Status: …"/>
                <Button Name="OpenA11yButton" Grid.Column="1" Margin="8,0,0,0"
                        Content="Open System Settings" IsVisible="False"/>
                <Button Name="RecheckA11yButton" Grid.Column="2" Margin="6,0,0,0"
                        Content="Re-check" IsVisible="False"/>
            </Grid>
        </Border>
```

- [ ] **Step 2: Drive the banner state from IPermissionGate**

In `src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs`:

Add `using System.Runtime.InteropServices;` to the using block.

In the constructor, after the existing button wiring (`this.FindControl<Button>("CancelButton")!.Click += ...;` line), add:

```csharp
        this.FindControl<Button>("OpenA11yButton")!.Click += (_, _) => OpenAccessibilitySettings();
        this.FindControl<Button>("RecheckA11yButton")!.Click += (_, _) => RefreshStatus();
```

Replace the `RefreshStatus()` method body with:

```csharp
    private void RefreshStatus()
    {
        var banner = this.FindControl<TextBlock>("StatusBanner");
        var border = this.FindControl<Border>("StatusBorder");
        var openBtn = this.FindControl<Button>("OpenA11yButton");
        var recheckBtn = this.FindControl<Button>("RecheckA11yButton");
        if (banner is null || border is null || openBtn is null || recheckBtn is null) return;

        var gate = _sp.GetRequiredService<IPermissionGate>();
        if (!gate.CanInjectInput)
        {
            banner.Text = "Accessibility permission required — KeepAwakeTool cannot keep you "
                        + "active until it is granted in System Settings ▸ Privacy & Security ▸ Accessibility.";
            border.Background = Avalonia.Media.Brushes.DarkOrange;
            openBtn.IsVisible = true;
            recheckBtn.IsVisible = true;
            return;
        }

        openBtn.IsVisible = false;
        recheckBtn.IsVisible = false;
        border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x22, 0x80, 0x80, 0x80));

        var scheduler = _sp.GetRequiredService<Scheduler>();
        var cfg = _sp.GetRequiredService<Func<AppConfig>>()();
        var idle = _sp.GetRequiredService<IIdleMonitor>().TimeSinceLastUserInput();
        banner.Text = "Status: " + StatusText.Build(
            scheduler.State,
            cfg.Power.ForceDisplayOffAfterInjection,
            cfg.Power.PowerSaveMode,
            cfg.Activity.IntervalSeconds,
            idle);
    }

    private void OpenAccessibilitySettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "open",
                "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
            { UseShellExecute = false });
        }
        catch { /* best-effort; banner stays until Re-check confirms grant */ }
    }
```

Note: `Border` is in `Avalonia.Controls` (already imported via `using Avalonia.Controls;`). `IPermissionGate` is in `KeepAwakeTool.Core.Platform` (already imported).

- [ ] **Step 3: Build and run default suite**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

Run: `dotnet test -c Release --filter "Category!=PlatformIntegration&Category!=Timing" -- xUnit.ParallelizeTestCollections=false`
Expected: 53 passing (App.Tests still green; on Windows the gate is always-true so the banner path is unchanged).

- [ ] **Step 4: Commit**

```bash
git add src/KeepAwakeTool.App/Views/SettingsWindow.axaml src/KeepAwakeTool.App/Views/SettingsWindow.axaml.cs
git commit -m "$(cat <<'EOF'
feat(app): Accessibility warning state in the window-level status banner

Reuses the always-visible StatusBanner; on macOS without Accessibility it
shows an orange warning with Open System Settings + Re-check. No-op on
Windows (gate always permits).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 20: Tray icon gray + tooltip suffix when untrusted

**Files:**
- Modify: `src/KeepAwakeTool.App/Tray/TrayIconController.cs`

- [ ] **Step 1: Override icon + tooltip when the gate denies input**

In `src/KeepAwakeTool.App/Tray/TrayIconController.cs`:

In `UpdateIcon(EngineState state)`, replace the `var asset = state switch { ... };` assignment with a gate-aware version:

```csharp
        var gate = _sp.GetRequiredService<IPermissionGate>();
        var asset = !gate.CanInjectInput
            ? "avares://KeepAwakeTool/Tray/Assets/icon-stopped.ico"
            : state switch
            {
                EngineState.Running   => "avares://KeepAwakeTool/Tray/Assets/icon-running.ico",
                EngineState.Paused    => "avares://KeepAwakeTool/Tray/Assets/icon-paused.ico",
                EngineState.PowerSave => "avares://KeepAwakeTool/Tray/Assets/icon-powersave.ico",
                _                     => "avares://KeepAwakeTool/Tray/Assets/icon-stopped.ico"
            };
```

In `ComposeTrayTooltip()`, replace the `return "KeepAwakeTool — " + StatusText.Build(...) + (_remote ? ... : "");` statement with:

```csharp
        var gate = _sp.GetRequiredService<IPermissionGate>();
        var a11y = gate.CanInjectInput ? "" : " · Accessibility permission required";
        return "KeepAwakeTool — " + StatusText.Build(
                   scheduler.State,
                   cfg.Power.ForceDisplayOffAfterInjection,
                   cfg.Power.PowerSaveMode,
                   cfg.Activity.IntervalSeconds,
                   idle)
               + a11y
               + (_remote ? " · RDP: display-off limited" : "");
```

`IPermissionGate` is in `KeepAwakeTool.Core.Platform` (already imported in this file).

- [ ] **Step 2: Build and run default suite**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors.

Run: `dotnet test -c Release --filter "Category!=PlatformIntegration&Category!=Timing" -- xUnit.ParallelizeTestCollections=false`
Expected: 53 passing.

- [ ] **Step 3: Commit**

```bash
git add src/KeepAwakeTool.App/Tray/TrayIconController.cs
git commit -m "$(cat <<'EOF'
feat(app): gray tray icon + tooltip suffix when input permission is denied

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

# Phase 4 — Docs + CI

## Task 21: macOS manual-smoke document

**Files:**
- Create: `docs/manual-smoke-macos.md`

- [ ] **Step 1: Write the checklist (mirrors the Windows manual-smoke set, spec §11/§13)**

Create `docs/manual-smoke-macos.md`:

```markdown
# KeepAwakeTool — macOS Manual Smoke Test (v2)

Run on macOS 14+ (Apple Silicon) from a self-contained publish:
`dotnet publish src/KeepAwakeTool.App -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish`

| # | Scenario | Steps | Expected |
|---|----------|-------|----------|
| 1 | First run without Accessibility | Launch app fresh | Tray icon appears **gray**; Settings shows the orange banner with **Open System Settings** + **Re-check**; no injection occurs |
| 2 | Grant Accessibility at runtime | Click **Open System Settings**, enable KeepAwakeTool, click **Re-check** | Banner returns to normal status; tray turns green; no app restart needed |
| 3 | Presence kept (default config) | Stay idle 15 min with Teams/Slack open | Status stays **Available**; idle countdown in tooltip resets each interval |
| 4 | Idle-anchored timing | Type a key during the countdown | Countdown resets; no injection until a full interval of real idle elapses |
| 5 | S1 display-off | Enable "Force display off (S1)", stay idle | Display sleeps shortly after each injection (brief blink), system stays awake |
| 6 | S3 power-save | Enable "Power-Save Mode (S3)" | Tray turns red; no synthetic input; system does not idle-sleep |
| 7 | Global hotkey | Set a hotkey, press it from another app | Pause/Resume toggles; an already-in-use combo shows the failure toast |
| 8 | Autostart | Enable "Start with login", reboot | App auto-launches; toggle reflects real SMAppService status |
| 9 | Resume from sleep | Close lid, reopen | Awake assertion is re-armed; presence keeps working |
| 10 | Single instance | Launch the app twice | Second launch exits immediately; first instance unaffected |
```

- [ ] **Step 2: Commit**

```bash
git add docs/manual-smoke-macos.md
git commit -m "$(cat <<'EOF'
docs: add macOS v2 manual smoke-test checklist

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 22: CI — add a macos-latest job

**Files:**
- Modify: `.github/workflows/ci.yml`

- [ ] **Step 1: Add a macOS build+test job (default suite only; integration + Timing excluded)**

In `.github/workflows/ci.yml`, add this job under `jobs:` (sibling of `build-test`, before `publish`):

```yaml
  build-test-macos:
    runs-on: macos-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      # Excludes the OS FileSystemWatcher timing test (nondeterministic in CI)
      # and the macOS PlatformIntegration tests (require a real interactive
      # session / Accessibility grant — run via the manual smoke instead).
      - run: dotnet test --no-build -c Release --filter "Category!=Timing&Category!=PlatformIntegration" --verbosity normal -- xUnit.ParallelizeTestCollections=false
```

- [ ] **Step 2: Add the non-parallel flag to the existing Windows job for consistency**

In the existing `build-test` job, replace its test line with:

```yaml
      - run: dotnet test --no-build -c Release --filter "Category!=Timing&Category!=PlatformIntegration" --verbosity normal -- xUnit.ParallelizeTestCollections=false
```

(Adds `&Category!=PlatformIntegration` and the mandatory non-parallel flag; behavior is otherwise identical on Windows.)

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "$(cat <<'EOF'
ci: add macos-latest build+test job; enforce non-parallel test collections

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

## Task 23: Final verification

**Files:** none (verification + finishing).

- [ ] **Step 1: Full clean build + default test suite on macOS**

Run: `dotnet build -c Release`
Expected: Build succeeded, 0 errors, 5 source projects + 3 test projects.

Run: `dotnet test -c Release --filter "Category!=PlatformIntegration&Category!=Timing" -- xUnit.ParallelizeTestCollections=false`
Expected: 53 passing (40 Core + 11 App + 2 new gate tests).

- [ ] **Step 2: macOS integration tests**

Run: `dotnet test tests/KeepAwakeTool.Platform.Mac.Tests -c Release --filter "Category=PlatformIntegration" -- xUnit.ParallelizeTestCollections=false`
Expected: 3 passing.

- [ ] **Step 3: macOS publish smoke (the deliverable artifact)**

Run: `dotnet publish src/KeepAwakeTool.App -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o /tmp/kat-osxpub 2>&1 | tail -3`
Expected: publish succeeds; `/tmp/kat-osxpub/KeepAwakeTool` exists. Confirm `ls /tmp/kat-osxpub | grep Platform` shows **no** `KeepAwakeTool.Platform.Win.dll` (single-file may fold it; if so run the non-single-file build check from Task 7 Step 3 with `-r osx-arm64`). Then `rm -rf /tmp/kat-osxpub`.

- [ ] **Step 4: Windows publish graph unaffected (final regression gate)**

Repeat Task 7 Steps 2–4. Expected: win-x64 publish resolves only `Platform.Win`, no `Platform.Mac.dll`.

- [ ] **Step 5: Hand off to the human for the real macOS smoke test**

Per ONBOARDING §3, the human runs `docs/manual-smoke-macos.md` on this machine (the real app, with the Accessibility prompt). Report build/test results + the publish path and request the manual smoke run. Do **not** claim v2 complete until the human confirms the smoke checklist (verification-before-completion).

---

## Notes for the implementer

- **P/Invoke is not unit-testable.** Mac interop correctness is proven by Task 18/23 integration tests + the human smoke run, not by unit tests. If an integration test crashes the runner (native signature mismatch), that is the expected failure mode — fix the signature, do not weaken the test (systematic-debugging: root cause first).
- **Windows v1 safety invariant:** Tasks 7 and 23 Step 4 are the regression gates. If `Platform.Mac.dll` ever appears in a `win-x64` output, stop and fix the conditional `ProjectReference` before continuing.
- **FluentAssertions is pinned to 7.2.2** repo-wide (the last free-licensed major; CLAUDE.md's "v8" note is stale). The new Mac test project must use 7.2.2 to match; test-only, never shipped.
- **Do not "fix" the Win11 tray-overflow quirk** — Windows-only, documented, irrelevant to macOS.
```
