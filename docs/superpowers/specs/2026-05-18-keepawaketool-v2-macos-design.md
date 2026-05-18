# KeepAwakeTool v2 (macOS) — Design Spec

**Date**: 2026-05-18
**Status**: Approved (brainstorm)
**Target stack**: .NET 10, Avalonia 12, C#
**Supersedes for macOS**: parts of `2026-05-14-keepawaketool-design.md` (the v1 design doc). The
v1 doc remains the historical record; **this spec is authoritative for macOS (v2)**. Revised v1
sections are called out in §10.

## 1. Goal

Port KeepAwakeTool to macOS (v2) behind the existing platform-abstraction layer, without
changing the shipped Windows v1 behavior. v1 (Windows) is built and in user smoke-test; the
platform-agnostic engine, scheduler, pump, config, ViewModels, and Avalonia UI are reused
unchanged. v2 is two coupled pieces of work:

1. **Restructure** `KeepAwakeTool.App` so it builds and runs on macOS (it currently targets
   `net10.0-windows`, is `WinExe`, hard-references `KeepAwakeTool.Platform.Win`, uses a
   Windows-only application manifest, and consumes the Windows-only
   `Microsoft.Win32.SystemEvents` API directly).
2. **Create `KeepAwakeTool.Platform.Mac`** implementing the Core platform interfaces with
   macOS APIs, plus a first-run **Accessibility-permission** flow (no Windows analog).

These are one spec because the restructure exists specifically to host `Platform.Mac`;
splitting them would create an untestable intermediate state.

## 2. Constraints & Non-Goals

- **Minimum macOS**: **14.0 (Sonoma)**, Apple Silicon (`osx-arm64`). This baseline lets
  autostart use the modern `SMAppService` API with no legacy LaunchAgents fallback.
- **Safety invariant**: the Windows v1 publish path must remain **byte-for-byte unaffected**.
  The Windows publish command and its bundled assemblies do not change.
- **In scope (v2)**: macOS implementations of all six Core platform interfaces, two new Core
  abstractions to decouple the App from Windows-only APIs, the build restructure, the
  Accessibility-permission detection + non-blocking Settings banner, a macOS single-instance
  guard, a macOS resume-from-sleep re-arm path, macOS manual-smoke documentation, and a
  `macos-latest` CI job.
- **Out of scope (YAGNI — explicit)**: screen-sharing / remote-session detection
  (`ISessionInfo` is stubbed to `false` on macOS — its only consumer is the RDP tooltip note),
  code-signing / notarization / `.pkg` installer, named-pipe / Unix-socket "focus existing
  instance" messaging (the running tray icon is the "already running" signal, as in v1),
  LaunchAgents plist fallback (macOS 14+ baseline ⇒ `SMAppService` only), tray-icon asset
  redesign, multi-profile config, telemetry, in-app updater.

## 3. Architecture / Project Layout

```
KeepAwakeTool.slnx
├── KeepAwakeTool.Core           // net10.0      — unchanged TFM; +2 new interfaces
├── KeepAwakeTool.Platform.Win   // net10.0-windows — unchanged; referenced on Windows builds only
├── KeepAwakeTool.Platform.Mac   // net10.0      — NEW; CoreGraphics/IOKit/Carbon/SMAppService P/Invoke
└── KeepAwakeTool.App            // net10.0      — was net10.0-windows; conditional platform refs
```

### 3.1 Platform-selection mechanism (Approach A — conditional ProjectReference)

The App `.csproj` references each platform project conditionally on the runtime identifier:

- `Platform.Win` is referenced when `$(RuntimeIdentifier)` starts with `win` **or** no RID is
  set.
- `Platform.Mac` is referenced when `$(RuntimeIdentifier)` starts with `osx` **or** no RID is
  set.
- The no-RID case (plain `dotnet build` / `dotnet test`) references **both**, so the solution
  always builds and Core tests run on either OS.
- A published, RID-specified build bundles **only** the matching platform assembly — no
  unused native interop is shipped, and the Windows publish output is unchanged.

`Platform.Win` keeps its `net10.0-windows` TFM (its Win32 P/Invoke requires it; it is only
referenced on Windows builds, so this is harmless). `Platform.Mac` is plain `net10.0` — macOS
interop is `[LibraryImport]` against system frameworks and needs no SDK platform gate.

### 3.2 App project changes

- TFM `net10.0-windows` → `net10.0`.
- `<OutputType>WinExe</OutputType>` → `Exe`.
- `<ApplicationManifest>app.manifest</ApplicationManifest>` global property removed; the
  manifest is re-applied via a **Windows-conditional** `<ApplicationManifest>` item so the
  Windows publish keeps it. `app.manifest` stays in the repo.
- `PackageReference Microsoft.Win32.SystemEvents` removed from `App` (Windows-only). Its one
  use (resume-from-sleep) moves behind `ISystemPowerEvents` (§4), whose Windows impl lives in
  `Platform.Win` where the package dependency is legitimate.
- Avalonia and `Microsoft.Extensions.*` package references are unchanged (already
  cross-platform).
- `KeepAwakeTool.slnx` gains the `Platform.Mac` project.

### 3.3 Two new Core abstractions

Both live in `KeepAwakeTool.Core` (it already owns every platform interface), and each gets a
Windows impl and a macOS impl selected by DI:

1. **`ISystemPowerEvents`** — `event Action? Resumed; void Start(); void Stop();`
   - Win impl (`Platform.Win`): wraps `Microsoft.Win32.SystemEvents.PowerModeChanged`; fires
     `Resumed` on `PowerModes.Resume`. Behavior identical to current v1.
   - Mac impl (`Platform.Mac`): IOKit `IORegisterForSystemPower` +
     `IONotificationPortGetRunLoopSource`; fires `Resumed` on `kIOMessageSystemHasPoweredOn`;
     acknowledges power messages with `IOAllowPowerChange`.
   - `App.axaml.cs` consumes `ISystemPowerEvents.Resumed → PowerModeController.Rearm()`
     instead of referencing `Microsoft.Win32.SystemEvents` directly.

2. **`ISingleInstanceGuard`** — `bool TryAcquire(); void Release();`
   - Win impl (`Platform.Win`): the current named-`Mutex` `Global\KeepAwakeTool`
     (`SingleInstanceGuard` logic moves behind this interface).
   - Mac impl (`Platform.Mac`): an exclusive lock file
     `~/Library/Application Support/KeepAwakeTool/instance.lock` held open with an advisory
     lock (`flock` / `FileShare.None`); `TryAcquire` fails if another instance holds it. Per
     v1 §9.4. (.NET named mutexes are process-local on macOS, so the mutex cannot work
     cross-instance there — hence the lock file.)
   - `Program.Main` resolves the guard via a minimal `RuntimeInformation.IsOSPlatform`
     switch *before* Avalonia starts (DI is not yet built at that point). v1 behavior is
     preserved: a second instance exits; `SignalExistingInstance()` stays a no-op.

### 3.4 DI selection (`ServiceRegistration`)

Replace the `throw` on non-Windows with:

```
if   (IsOSPlatform(Windows)) { register Windows impls of the 6 interfaces + ISystemPowerEvents + IPermissionGate }
elif (IsOSPlatform(OSX))     { register macOS  impls of the 6 interfaces + ISystemPowerEvents + IPermissionGate }
else                          throw PlatformNotSupportedException
```

The six existing interfaces are `IInputSimulator`, `IIdleMonitor`, `IPowerManager`,
`IAutoStartManager`, `ISessionInfo`, `IGlobalHotkeyService`. `IClock`/`SystemClock` is already
cross-platform and unchanged. `IPermissionGate` is new (§5).

## 4. `KeepAwakeTool.Platform.Mac` — Interface Implementations

All via `[LibraryImport]` P/Invoke to system frameworks; no NuGet native dependencies.
Every created CoreFoundation/CoreGraphics object is released (`CFRelease`).

| Interface | Impl | macOS mechanism |
|---|---|---|
| `IInputSimulator` | `MacInputSimulator` | CoreGraphics. Mouse: `CGEventCreateMouseEvent(NULL, kCGEventMouseMoved, point, 0)` — Invisible = `(0,0)` delta; Jiggle = a single relative hop of `±jigglePixels` whose sign **alternates each injection** (mirrors the Windows no-drift behavior). Keyboard: `CGEventCreateKeyboardEvent` down+up pair for F13/F14/F15 (virtual keycodes **105 / 107 / 113**). Post via `CGEventPost(kCGHIDEventTap, evt)`. Silently no-ops if Accessibility not trusted (the engine gate in §5 prevents reaching here untrusted). |
| `IIdleMonitor` | `MacIdleMonitor` | `CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateHIDSystemState, kCGAnyInputEventType)` → `TimeSpan.FromSeconds(...)`. Synthetic input posted via `CGEventPost` also resets this timer, so the idle-anchored cadence (v1 §6) holds identically on macOS. |
| `IPowerManager` | `MacPowerManager` | Awake assertion: `IOPMAssertionCreateWithName(kIOPMAssertPreventUserIdleSystemSleep, kIOPMAssertionLevelOn, CFSTR("KeepAwakeTool"), out id)`; release with `IOPMAssertionRelease(id)`. Idempotent: `KeepSystemAwake(true)` when already asserted is a no-op; `(false)` when not asserted is a no-op. Display-off: `Process.Start("/usr/bin/pmset", "displaysleepnow")`, fire-and-forget; failures are logged via the platform log callback, never thrown (matches v1 §10.1 "log and continue"). |
| `IAutoStartManager` | `MacAutoStartManager` | `SMAppService.mainApp` (macOS 14+). `IsEnabled` ← `status == SMAppServiceStatusEnabled`; `Enable()` → `[service registerAndReturnError:]`; `Disable()` → `[service unregisterAndReturnError:]`. Invoked through the Objective-C runtime via `objc_msgSend` P/Invoke. Errors logged, not thrown. |
| `IGlobalHotkeyService` | `MacGlobalHotkeyService` | Carbon `RegisterEventHotKey` on a dedicated **CFRunLoop thread** named `"KAT-HotkeyPump"` (structurally mirrors `WindowsGlobalHotkeyService`'s hidden-message-pump thread). Install a Carbon event handler for `kEventHotKeyPressed`; run `CFRunLoopRun`. `TryRegister` marshals the actual `RegisterEventHotKey` onto the pump thread and returns `true` once queued; the real success/failure is raised **asynchronously** via `RegistrationResult` (true = registered, false = e.g. combo already in use), exactly per the Core contract. `Unregister` → `UnregisterEventHotKey` on the pump thread. The `Hotkey` chord maps to a Carbon modifier mask + virtual keycode. Disposed on app exit. |
| `ISessionInfo` | `MacSessionInfo` | `IsRemoteSession => false` (stub, per v1 §14). The only consumer is the RDP tooltip note; screen-sharing detection is explicitly deferred (YAGNI). |
| `ISystemPowerEvents` | `MacSystemPowerEvents` | IOKit `IORegisterForSystemPower` (see §3.3). Shares the run-loop-thread infrastructure pattern used by the hotkey pump. |

### 4.1 Accessibility permission probe

A static helper `MacAccessibility` (not an interface — it has no Windows counterpart):

- `IsTrusted()` → `AXIsProcessTrustedWithOptions(NULL)` — checks without prompting.
- `RequestTrust()` → `AXIsProcessTrustedWithOptions({ kAXTrustedCheckOptionPrompt: true })`
  — triggers the system Accessibility prompt.

CoreGraphics synthetic input silently fails when the process is not Accessibility-trusted, so
injection must be gated on trust (§5).

## 5. Engine Gating for Accessibility — `IPermissionGate`

New Core abstraction so the cross-platform engine never calls an OS API directly:

```csharp
public interface IPermissionGate
{
    bool CanInjectInput { get; }   // Win: always true. Mac: MacAccessibility.IsTrusted()
}
```

- Win impl: returns `true` always (no analog; zero behavior change on Windows).
- Mac impl (`MacPermissionGate`): returns `MacAccessibility.IsTrusted()` (re-evaluated on
  access, so granting permission at runtime is picked up on the next pump tick — no restart).
- `ActivityEngine` treats `CanInjectInput == false` as an additional **injection gate**,
  exactly like the existing S3 / out-of-hours / hotkey-paused gates in v1 §6: the tick
  returns before injecting. **No new tool state is added** — `Scheduler.EvaluateState()` is
  unchanged. The gate also drives the tray icon (gray) and tooltip suffix, and the Settings
  banner (§6).

This keeps the gate decision unit-testable with a fake `IPermissionGate` and preserves the
v1 state machine (§7 of the v1 doc) verbatim.

## 6. First-Run Accessibility UX (macOS)

When `IPermissionGate.CanInjectInput == false` on macOS:

- The app starts normally — single-instance guard, tray, and Settings all function.
- **No blocking modal.** No "first run ever" tracking.
- Tray icon shows the existing **gray** ("Stopped"-style) visual; the live tooltip appends
  `" — Accessibility permission required"`. `ActivityEngine` never injects (gated per §5).
- The **existing window-level `StatusBanner`** in `SettingsWindow.axaml` (the top
  `DockPanel.Dock="Top"` border above the `TabControl`, introduced for unified live status)
  is **reused**: it is already always visible across all five tabs. When untrusted on macOS
  it switches to a warning state with explanatory text (e.g. *"Accessibility permission
  required — KeepAwakeTool cannot keep you active until it is granted"*) and exposes two
  actions in/beside that banner region:
  - **`[Open System Settings]`** — opens the deep link
    `x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility` (reliably
    lands the user in the Accessibility pane). Additionally, `MacAccessibility.RequestTrust()`
    is called **once at first launch when untrusted** so the app is pre-listed in that pane
    (toggle present, just off) — the button itself only opens the pane.
  - **`[Re-check]`** — re-reads `IsTrusted()`; on success the banner returns to its normal
    live-status content and the engine begins injecting on the next pump tick (no restart).
- The banner `Border` (currently a single `TextBlock`) is extended to host the two buttons
  in the macOS-untrusted state. This is the **only** new UI surface in v2; it is an
  OS-agnostic binding whose *content/state* varies — the layout is not branched per OS.
- On Windows, or when trusted on macOS, the banner behaves exactly as today (unified live
  S1/S3/status text). Accessibility state is "just another status" the existing banner
  reflects.

All other UI — five Settings tabs, tray `NativeMenu`, `ToastService`, `TrayIconController`
state-driven icon + live idle-countdown tooltip, the Record-hotkey capture flow — is reused
unchanged (Avalonia 12 is already cross-platform; nothing in the ViewModels or XAML is
Windows-specific). Existing `.ico` tray assets are reused as-is; if a smoke test shows macOS
rendering issues, that is a flagged risk to address, not a planned task.

## 7. Data Flow / State Machine

Unchanged from v1 §6 and §7. `EnginePump` → `Scheduler.RunOneTickAsync` →
`ActivityEngine.TickAsync` is platform-agnostic and reused verbatim. The only addition is the
`IPermissionGate` injection gate (§5), which sits alongside the existing S3 / schedule /
hotkey-pause gates and does not alter `Scheduler.EvaluateState()` or the visible tool states
(Stopped / Running / Paused / PowerSave). Idle-anchored cadence (v1 §6) holds identically:
`CGEventPost` resets the macOS HID idle timer just as `SendInput` resets the Windows one.

## 8. Configuration

Unchanged schema (v1 §8). Config path on macOS is already
`~/Library/Application Support/KeepAwakeTool/config.json` (v1 §8) — `ConfigurationStore` uses
`Environment.SpecialFolder.ApplicationData`, which resolves correctly on macOS, so no code
change. The single-instance lock file (§3.3) lives in the same directory.

## 9. Error Handling & Edge Cases (macOS specifics)

Inherits v1 §10 principles (native failure ⇒ log via the platform log callback and continue;
next tick retries; engine-tick exceptions caught in `EnginePump`). macOS specifics:

- **Accessibility permission missing** — engine does not inject, tray icon gray, banner with
  CTA (§6). Granting at runtime is picked up on the next pump tick (no restart). **Revises v1
  §9.3 / §10.1**: non-blocking banner, not a modal, and reusing the window-level status
  banner rather than a General-tab element.
- **Resume from sleep** — `ISystemPowerEvents.Resumed` (IOKit `IORegisterForSystemPower`) →
  `PowerModeController.Rearm()`, equivalent to the Windows `SystemEvents` path.
- **Display-off failure** (`pmset` missing/non-zero) — logged, not thrown; injection and
  awake-assertion continue.
- **`SMAppService` register/unregister error** — logged; the autostart toggle reflects the
  real `status` on next read so the UI cannot get permanently out of sync.
- **App exit / crash** — `IOPMAssertionRelease` on graceful exit; on hard crash the OS
  reclaims the assertion when the process dies (safe by default, parity with the Windows
  `ES_CONTINUOUS`-cleared-on-exit behavior). The single-instance lock file's advisory lock is
  released by the OS on process death.
- **Hotkey combo already in use** — Carbon `RegisterEventHotKey` failure → `RegistrationResult(false)`
  → existing `ToastService` toast (v1 §10.1), no new code path.

## 10. Revised v1 Spec Sections (for traceability)

This spec supersedes, for macOS, the following parts of `2026-05-14-keepawaketool-design.md`:

- **§9.3 (First-Run macOS)** — was "modal explains why…"; now a non-blocking, always-visible
  **window-level status banner** reusing the existing `StatusBanner`, with
  `[Open System Settings]` + `[Re-check]`. Engine gated, tray gray.
- **§10.1 (macOS Accessibility bullet)** — clarified to the banner behavior above; "engine
  refuses to start" means "engine does not inject" (the app and tray still run).
- **§14 (macOS Implementation Notes)** — concretized: minimum macOS 14, `SMAppService`
  autostart (no LaunchAgents fallback), Carbon `RegisterEventHotKey` for the hotkey,
  `pmset displaysleepnow` subprocess for display-off, `ISessionInfo` stubbed `false`, plus
  the two new Core abstractions (`ISystemPowerEvents`, `ISingleInstanceGuard`) and
  `IPermissionGate`, none of which existed in the v1 interface list.

The v1 design doc itself is left unmodified as the historical record.

## 11. Testing Approach

- **Core unit tests** (`KeepAwakeTool.Core.Tests`) — run on macOS unchanged (51 currently
  green). Add `ActivityEngine` tests with a fake `IPermissionGate`: untrusted ⇒ no injection
  even when all other gates pass; trusted ⇒ normal injection. Same fake-driven style as the
  existing `ActivityEngineTests`.
- **Platform.Mac integration tests** — `[Trait("Category","PlatformIntegration")]`,
  macOS-only, parallel to the Windows integration tests (v1 §11), excluded from default
  `dotnet test`: `MacIdleMonitor` returns a plausible `TimeSpan`; `CGEventPost` succeeds;
  `MacAccessibility.IsTrusted()` returns a bool without throwing.
- **Non-parallel flag still required**: `dotnet test -c Release -- xUnit.ParallelizeTestCollections=false`
  (the `FileSystemWatcher` flake is OS-agnostic).
- **Manual smoke** — new `docs/manual-smoke-macos.md` mirroring the Windows checklist:
  Teams/Slack stays Available across 15 min real idle; S1 display-off blink via `pmset`;
  global hotkey toggles pause from any focused app; `SMAppService` autostart survives reboot;
  Accessibility revoked ⇒ gray tray + banner ⇒ grant ⇒ auto-resumes without restart. Per
  ONBOARDING §3, the human runs this on the actual Mac.
- No automated UI tests (parity with v1).

## 12. Build, Publish, Distribution

- `dotnet build -c Release` (no RID) builds all five projects on macOS (both platform
  projects referenced) — solution always green.
- macOS publish:
  `dotnet publish src/KeepAwakeTool.App -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish`
  → bundles `Platform.Mac` only.
- **Windows publish command and output unchanged** → bundles `Platform.Win` only → v1 shipped
  path byte-for-byte unaffected (the core safety property of Approach A).
- **CI**: add a `macos-latest` job (build + Core tests, non-parallel) alongside the existing
  `windows-latest`, per v1 §12.
- Distribution: portable self-contained binary, as v1. Code-signing / notarization / `.pkg`
  are out of scope (§2); first launch will show a Gatekeeper prompt the user accepts.

## 13. v2 Acceptance Criteria

- `dotnet build -c Release` and `dotnet test -c Release -- xUnit.ParallelizeTestCollections=false`
  are green on macOS; the existing 51 tests still pass plus the new permission-gate tests.
- Windows publish output is unchanged versus pre-v2 (same bundled assemblies; spot-verified).
- macOS publish produces a runnable self-contained `osx-arm64` app bundling only
  `Platform.Mac`.
- With Accessibility granted: default config keeps Teams/Slack "Available" across 15 minutes
  of real idle on macOS 14+; idle-anchored timing behaves as on Windows (real input resets
  the countdown).
- S1 ON: display turns off shortly after each injection (`pmset displaysleepnow`).
- S3 ON: tray red, no synthetic input, system stays awake (`IOPMAssertion` held).
- Global hotkey (enabled) toggles pause from any focused app; an in-use combo surfaces the
  failure toast.
- Autostart toggle registers/unregisters via `SMAppService`; app starts after reboot.
- Accessibility revoked ⇒ tray gray + always-visible banner with working
  `[Open System Settings]` / `[Re-check]`; granting resumes injection on the next pump tick
  without restarting the app.
- Resume from sleep re-arms the awake assertion (`IORegisterForSystemPower` path).
- Second app instance exits cleanly (lock-file guard).
