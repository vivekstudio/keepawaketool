# KeepAwakeTool — Design Spec

**Date**: 2026-05-14
**Status**: Approved (brainstorm)
**Target stack**: .NET 10, Avalonia 12, C#

## 1. Goal

A small background utility that keeps the OS active so presence-aware apps (Microsoft Teams, Slack, etc.) continue to report the user as Active during long idle periods, while optionally turning the display off to reduce battery drain and OLED burn-in.

Windows is the v1 target; macOS is the planned v2 port. The same code-base supports both via a thin platform-abstraction layer.

## 2. Constraints & Non-Goals

- **In scope (v1)**: Windows-only, single self-contained binary, system-tray + settings-window UX, JSON config, autostart-on-login, global hotkey, working-hours schedule.
- **In scope (v2)**: macOS implementation behind the same interfaces.
- **Out of scope**: macOS in v1, telemetry, crash reporting, multiple config profiles, cloud sync, installer (MSI/.pkg), in-app updater, per-process input-API hooking (DLL injection — invasive and EDR-flagged).

## 3. Architectural Reality Check (sleep + presence tradeoff)

Presence-aware apps detect "Away" via the OS input-idle timer (`GetLastInputInfo` on Windows, `CGEventSourceSecondsSinceLastEventType` on macOS). The only way to reset that timer is to inject synthetic mouse/keyboard input. **Injecting input also wakes the display** — both OSes share the same input-event plumbing for both effects.

This drives the user-visible feature set:

Timing is **idle-anchored**: the engine injects only once the OS idle timer has reached `IntervalSeconds` of no real user input, then repeats every `IntervalSeconds` while the user stays idle (see §6). It is not a fixed periodic injector.

| Mode | Behavior | Display |
|---|---|---|
| **Normal** (default) | When idle ≥ `IntervalSeconds`, inject input (resetting the idle timer); system stays awake | Display follows the OS sleep policy |
| **S1: Force display off after injection** (toggle, default OFF) | On each injection, ~200 ms later force display off | Brief "blink" each injection; display off otherwise |
| **S3: Power-Save Mode** (toggle, default OFF) | No injection; hold only `ES_SYSTEM_REQUIRED` / `IOPMAssertion` | Display sleeps normally; Teams **will** mark Away |

S1 and S3 are independent toggles. S3 overrides S1 (no injection ⇒ nothing to blink).

## 4. Tech Stack

- **.NET 10**, **C#**.
- **Avalonia 12** for the settings window and tray (cross-platform).
- **Microsoft.Extensions.DependencyInjection** for service wiring.
- **System.Text.Json** for configuration.
- No external native dependencies beyond OS-provided libraries.
- Single-file self-contained publish per platform (`dotnet publish -r win-x64 --self-contained`).

## 5. Solution / Project Layout

```
KeepAwakeTool.sln
├── KeepAwakeTool.Core           // platform-agnostic engine, config, scheduling, interfaces
├── KeepAwakeTool.Platform.Win   // Win32 P/Invoke implementations of Core interfaces
├── KeepAwakeTool.Platform.Mac   // (v2) CoreGraphics + IOKit P/Invoke
└── KeepAwakeTool.App            // Avalonia: tray icon, settings window, host
```

### 5.1 Core Interfaces (live in `KeepAwakeTool.Core`)

```csharp
public interface IInputSimulator
{
    void MoveMouse(MouseMode mode, int jigglePixels); // Invisible (0,0 delta) or Jiggle (±jigglePixels)
    void SendKey(VirtualKey key);                      // F13 / F14 / F15
}

public interface IPowerManager
{
    void KeepSystemAwake(bool on);       // ES_CONTINUOUS|ES_SYSTEM_REQUIRED or IOPMAssertion
    void ForceDisplayOff();              // SC_MONITORPOWER / pmset displaysleepnow
}

public interface IIdleMonitor
{
    TimeSpan TimeSinceLastUserInput();   // GetLastInputInfo / CGEventSourceSeconds...
}

public interface ISessionInfo
{
    bool IsRemoteSession { get; }        // SM_REMOTESESSION (Win) / equivalent (Mac, v2)
}

public interface IAutoStartManager
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}

public interface IGlobalHotkeyService
{
    bool TryRegister(Hotkey hotkey, Action onPressed);
    void Unregister();
    // Raised from the hotkey pump thread after RegisterHotKey completes.
    // true = registered; false = failed (e.g. combo already in use).
    // TryRegister returns true once the request is queued; the real
    // RegisterHotKey result arrives asynchronously via this event.
    event Action<bool>? RegistrationResult;
}
```

There is also a Core `IClock` abstraction (`SystemClock` impl; `LocalNow` + `DelayAsync`) used by the engine/scheduler so timing is testable.

The DI container (`KeepAwakeTool.App.Composition.ServiceRegistration`) selects the Windows (or, in v2, macOS) implementation at startup, guarded by `RuntimeInformation.IsOSPlatform(...)`.

### 5.2 Core Components

- **`ActivityEngine`** — stateless w.r.t. timing; exposes `TickAsync(ct)` which runs the idle-anchored decision tree (Section 6). It does **not** own a timer. It tracks only `_injectionCount` (for keystroke cadence) plus the `HotkeyPaused` / `WithinWorkingHours` flags set by the `Scheduler`. Raises an `Injected` event on each injection (used for the optional tray heartbeat flash).
- **`EnginePump`** (App layer) — owns the loop. A background `Task` that, every ~1 s, calls `Scheduler.RunOneTickAsync`; exceptions are logged via `FileLogger` and the loop continues. There is no `PeriodicTimer`.
- **`PowerModeController`** — wires S1/S3 into `IPowerManager` state (system-awake assertion) and re-arms it on resume from sleep.
- **`ConfigurationStore`** — atomic read/write of `config.json`, `FileSystemWatcher`-based reactive `Changed` events, and corrupt-file quarantine (`.corrupt.<timestamp>.json` backup + defaults) with `LastCorruptBackupPath` / `CorruptQuarantined` signals.
- **`Scheduler`** — composes `ActivityEngine` + working-hours window + hotkey-pause flag + S3 into a unified `Running` / `Paused` / `PowerSave` / `Stopped` state. Each pump tick it refreshes config, recomputes `WithinWorkingHours`, re-evaluates state, and only calls `ActivityEngine.TickAsync` when `Running`. (Out-of-hours does **not** flip the tool to `Stopped`; the engine simply doesn't inject — see §7.)
- **`IClock`** / **`SystemClock`** — injected time source so the 200 ms S1 delay and schedule comparisons are deterministic in tests.

### 5.3 Platform.Win Implementations

- **`WindowsInputSimulator`** — `SendInput` with `INPUT_MOUSE` and `INPUT_KEYBOARD` (KEYDOWN + KEYUP pair). Invisible = a `(0,0)` relative move. Jiggle = a **single relative hop of `±jigglePixels`** whose sign alternates on each injection (a +N-then-−N round-trip in one call nets to zero and Windows coalesces/doesn't repaint it for an idle cursor, so it was invisible; the alternating single hop always relocates the cursor — visible — while oscillating within `jigglePixels` so it never drifts).
- **`WindowsPowerManager`** — `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` for awake (cleared with `ES_CONTINUOUS` alone). Display-off uses `SendMessageTimeoutW(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2, SMTO_ABORTIFHUNG, 1000ms)` — `WM_SYSCOMMAND` must be **sent**, not posted (a broadcast `PostMessage` of it is dropped); the timeout + `ABORTIFHUNG` keeps the engine pump thread from hanging on an unresponsive top-level window.
- **`WindowsIdleMonitor`** — `GetLastInputInfo` + `GetTickCount` delta.
- **`WindowsSessionInfo`** — `GetSystemMetrics(SM_REMOTESESSION)` to detect an RDP/remote session.
- **`WindowsAutoStartManager`** — registry value (quoted exe path) under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- **`WindowsGlobalHotkeyService`** — owns a single **persistent hidden message-pump window** (`HiddenMessageWindow`, message-only, on its own background "KAT-HotkeyPump" thread). `TryRegister`/`Unregister` post `WM_APP` messages so the actual `RegisterHotKey`/`UnregisterHotKey` always run on the pump thread; `WM_HOTKEY` (a thread message) is handled directly in the pump loop and invokes the callback. The real `RegisterHotKey` success/failure is surfaced asynchronously via `RegistrationResult`.

### 5.4 App (Avalonia)

- **`Program.cs`** — entry point. Installs `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` logging, then enforces single instance via `SingleInstanceGuard` (named `Mutex` `Global\KeepAwakeTool`) before starting Avalonia. (`SignalExistingInstance` is a v1 no-op — the existing tray icon stands in for "already running"; a named-pipe focus message is a future enhancement.)
- **`ServiceRegistration`** — builds the `Microsoft.Extensions.DependencyInjection` container: config store, `Func<AppConfig>` snapshot provider, `IClock`, the Windows platform implementations (guarded by an OS check that throws on non-Windows), `ActivityEngine`, `Scheduler`, `PowerModeController`, `EnginePump`, `ToastService`, `FileLogger`.
- **`App.axaml.cs`** — `OnFrameworkInitializationCompleted` wires it together: starts `PowerModeController`, the `TrayIconController`, applies theme via `RequestedThemeVariant`, starts `EnginePump`, subscribes to `ConfigurationStore.Changed` to re-apply autostart/hotkey/theme (idempotent guards), handles `SystemEvents.PowerModeChanged` (resume → `PowerModeController.Rearm`), and wires `ToastService` to the hotkey-registration-failure and corrupt-config signals. Lifecycle is logged via `FileLogger`.
- **`TrayIconController`** — Avalonia `TrayIcon`; state-driven icon (stopped/running/paused/powersave + optional heartbeat-flash icon), live idle-countdown tooltip (`DispatcherTimer`, 1 s), RDP tooltip note. Left-click opens Settings; context menu = **Pause/Resume**, checkable **Force display off (S1)**, checkable **Power-Save Mode (S3)**, **Settings…**, **Quit**.
- **`SettingsWindow`** — 640×600, resizable (min 480×420), five tabs: General / Activity / Power / Schedule / Hotkey; footer `[Apply] [OK] [Cancel]`.
- **`ToastService`** — best-effort bottom-right toast window (auto-closes after 6 s); never crashes the app.
- **`FirstRunPermissions`** (macOS, v2) — guided Accessibility-permission flow.

## 6. Data Flow — Single Tick

`ActivityEngine` is polled every ~1 s by `EnginePump`. The engine injects only when system idle ≥ `IntervalSeconds`:

```
poll (every ~1 s)
 ├─ S3 (Power-Save) ON?                             ──► return
 ├─ Schedule enabled && out of hours?               ──► return
 ├─ HotkeyPaused?                                   ──► return
 ├─ IdleMonitor.TimeSinceLastUserInput < Interval?  ──► return (idle-anchored gate)
 ├─ InputSimulator.MoveMouse(config.MouseMode)
 ├─ injectionCount++
 ├─ if KeystrokeEnabled && (injectionCount % EveryN == 0)
 │     InputSimulator.SendKey(config.Key)
 ├─ if ForceDisplayOff (S1)
 │     await Task.Delay(200ms); PowerManager.ForceDisplayOff()
 └─ TrayIcon.FlashHeartbeat()        (if enabled in config)
```

**Idle-anchored cadence**: `SendInput` (synthetic mouse/key) also resets the OS idle timer (`GetLastInputInfo`). So gating injection on `idle >= IntervalSeconds` naturally produces: user stops → exactly `Interval` later the first keep-alive fires → then every `Interval` while still idle → any real input resets the clock. No separate threshold needed.

**Atomicity**: each tick awaits fully before the next poll fires.

## 7. Tool-Level State Machine

```
                  ┌──────────────┐
   App start ──►  │   Stopped    │   gray icon
                  └──────┬───────┘
                         │ user: Start
                         ▼
                  ┌──────────────┐
                  │   Running    │   green icon
                  └──┬────────┬──┘
        hotkey-pause │        │ S3 toggle ON
                     ▼        ▼
            ┌────────────┐  ┌──────────────────┐
            │   Paused   │  │  PowerSave (S3)  │  red icon
            │ yellow icon│  │  system-awake    │
            └────────────┘  │  only            │
                            └──────────────────┘
```

Transitions: tray menu commands, global hotkey (Running ↔ Paused), S3 toggle (Running ↔ PowerSave). The visible tool state is computed by `Scheduler.EvaluateState()` from S3 and the hotkey-pause flag only — it is `PowerSave` if S3 is on, else `Paused` if hotkey-paused, else `Running`; `Stopped` is the pre-`Start()` state. The **working-hours schedule does not change the tool state**: when `schedule.enabled` and the current time is outside the window, the tool stays `Running` but `ActivityEngine` is gated (`WithinWorkingHours == false`) so no injection occurs; when back inside the window injection resumes automatically. (The earlier "Stopped at endTime" model was not implemented this way.)

## 8. Configuration

**Locations**:
- Windows: `%AppData%\KeepAwakeTool\config.json`
- macOS: `~/Library/Application Support/KeepAwakeTool/config.json`

**Schema (v1)**:

```jsonc
{
  "schemaVersion": 1,
  "activity": {
    "intervalSeconds": 60,              // idle seconds before first injection and repeat cadence (10..240)
    "mouse": {
      "mode": "Invisible",              // "Invisible" | "Jiggle"
      "jigglePixels": 1                 // 1..10, used only when mode=Jiggle
    },
    "keystroke": {
      "enabled": true,
      "key": "F15",                     // "F13" | "F14" | "F15"
      "everyNthCycle": 3                // 1..10
    }
  },
  "power": {
    "forceDisplayOffAfterInjection": false, // S1 toggle (default OFF; user opts in for the brief-blink behavior)
    "powerSaveMode": false                  // S3 toggle (overrides S1 and injection)
  },
  "schedule": {
    "enabled": false,
    "startTime": "09:00",
    "endTime": "18:00",
    "days": ["Mon", "Tue", "Wed", "Thu", "Fri"]
  },
  "hotkey": {
    "enabled": false,
    "combination": "Ctrl+Alt+P"
  },
  "startup": {
    "autoStartOnLogin": false,
    "startMinimizedToTray": true
  },
  "ui": {
    "theme": "System",                  // "System" | "Light" | "Dark"
    "showHeartbeatAnimation": false
  }
}
```

**Persistence rules**:
- Atomic write (temp file + rename); never half-written.
- Numeric fields clamped to allowed range on load.
- Unknown enum values fall back to default (forward-compat).
- Missing fields filled with defaults on load.
- Missing file at startup ⇒ defaults written before app proceeds.

## 9. UI Surface

### 9.1 Tray Icon

- **Icon state**: gray (Stopped), green (Running), yellow (Paused), red (PowerSave/S3); plus a brief heartbeat-flash icon on each injection when `ui.showHeartbeatAnimation` is on.
- **Tooltip**: `"KeepAwakeTool — Running — next activity in 47s"`, live-updating each second from the idle timer; appends `" — RDP: display-off limited"` in a remote session.
- **Left-click**: open Settings window (or focus existing).
- **Right-click menu** (the actual `NativeMenu`):

```
Pause / Resume                       (label toggles with state)
─────────────
Force display off (S1)               (checkable)
Power-Save Mode (S3)                 (checkable)
─────────────
Settings…
─────────────
Quit
```

(There is no status-header item, no "Start with Windows" item, and no "About" item in v1 — autostart lives on the General tab.)

### 9.2 Settings Window

640×600, resizable (min 480×420), five tabs:

1. **General** — interval `NumericUpDown` (10–240 s, step 5; "idle time before injection and repeat cadence"), "Start with Windows", "Start minimized to tray", theme combo (System/Light/Dark).
2. **Activity** — mouse mode (Invisible / Jiggle); jiggle pixel count (visible only when Jiggle); keystroke block (enabled, key dropdown F13/F14/F15, every-Nth cycle numeric).
3. **Power** — two toggles with short explainers: "Force display off after each injection (S1)" and "Power-Save Mode (S3) — disables presence injection".
4. **Schedule** — enabled checkbox; time-range picker; weekday checkboxes (visible only when enabled).
5. **Hotkey** — enabled checkbox; a **Record…** button that captures a key chord live (press a modifier + key; release commits, **Esc** cancels and restores the previous combo); the captured combination is shown next to the button; a validation message line (e.g. "Add a modifier", "Key not supported"). The hotkey is captured via real key events — it is not free-typed.

Footer buttons: `[Apply]` `[OK]` `[Cancel]`. Apply writes config and stays open.

### 9.3 First-Run (macOS only, v2)

If Accessibility permission missing, modal explains why the app needs it, a button opens **System Settings → Privacy & Security → Accessibility**, and a "Re-check" button reloads the permission state.

### 9.4 Single-Instance Enforcement

- Windows: named `Mutex` `Global\KeepAwakeTool` (`SingleInstanceGuard`, acquired in `Program.Main` before Avalonia starts).
- macOS (v2): lock file in `~/Library/Application Support/KeepAwakeTool/`.
- v1 behavior: a second launch simply exits (the existing tray icon is the visible "already running" signal). Signalling the existing instance to focus its Settings window via a named pipe (Windows) / Unix socket (macOS) is a deferred enhancement — `SignalExistingInstance()` is currently a no-op.

## 10. Error Handling & Edge Cases

### 10.1 Error Handling Principles

- **Native API failures** (`SendInput` returns 0, `SetThreadExecutionState` returns 0, `RegisterHotKey` fails) — log via `FileLogger` and continue; next tick retries. Engine-tick exceptions are caught in `EnginePump` and logged; the loop keeps running.
- **Hotkey registration failure** — **implemented**: `IGlobalHotkeyService.RegistrationResult(false)` triggers a `ToastService` toast ("Hotkey '…' could not be registered. It may already be in use…"); the user can pick a different combo.
- **Config file corrupt/unreadable** — **implemented**: `ConfigurationStore` writes a `.corrupt.<timestamp>.json` backup, writes defaults, continues, and raises `LastCorruptBackupPath` (startup) / `CorruptQuarantined` (runtime FSW reload); `App` shows a `ToastService` toast in both cases.
- **macOS Accessibility permission missing** (v2) — engine refuses to start, tray icon stays gray, banner in Settings with "Open System Settings" CTA.
- **Schedule misconfig** (start >= end) — treat as "all day", warn in UI.
- **Unhandled exceptions** — caught at `App` level, logged to `logs/keepawaketool-YYYYMMDD.log`, app keeps running. The tray icon must never disappear silently.

### 10.2 Edge Cases

- **System wake from sleep** — `SystemEvents.PowerModeChanged` (Win) / `IOPMRegisterForSystemPower` (Mac, v2) re-arms timer and resets tick counter.
- **Workstation locked** — engine keeps running; useful for in-meeting-on-lock-screen scenarios. S1 display-off still works through lock.
- **Multiple monitors** — display-off is system-wide on both OSes (Win `SC_MONITORPOWER`, macOS `pmset displaysleepnow`).
- **RDP / remote session** (Win) — if `GetSystemMetrics(SM_REMOTESESSION)` is true, surface a tooltip note; injection still works but local display-off is not meaningful.
- **App crash mid-S1** — `SetThreadExecutionState(ES_CONTINUOUS)` is cleared by the OS on process exit; safe by default.
- **Tray menu in Win11 overflow flyout (known limitation)** — when the tray icon sits in the Windows 11 "hidden icons" overflow flyout, the right-click `NativeMenu` can orphan/hang on hover: the system overflow flyout auto-closes on focus loss and `Avalonia.Win32`'s tray menu does not perform the `SetForegroundWindow` + `PostMessage(WM_NULL)` tracking dance `TrackPopupMenu` requires, so the menu is left without focus tracking. This is a framework-level limitation, not app code (the app only assigns `TrayIcon.Menu`). **Workaround:** pin the icon to the always-visible tray (Taskbar settings → other system tray icons → enable KeepAwakeTool); pinned icons are not in the focus-fragile flyout and the menu behaves correctly. A true in-code fix would require replacing `NativeMenu` with a custom owned popup, which Avalonia's `TrayIcon` does not cleanly support (no right-click hook) — deferred.

## 11. Testing Approach

- **Unit tests** (Core, no platform deps) — `ActivityEngineTests` against fake `IInputSimulator` / `IIdleMonitor` / `IPowerManager`:
  - Idle-anchored gate: no injection when `TimeSinceLastUserInput < IntervalSeconds`; injects when `>= IntervalSeconds`.
  - S3 ON ⇒ no injection.
  - Working-hours window respected.
  - Hotkey-paused state respected.
  - Keystroke fires only every Nth *injection* (not every Nth poll).
  - S1 ⇒ `ForceDisplayOff` called ~200 ms after injection.
  - Config-change events are picked up on the next poll.
- **Platform integration tests** (Windows; macOS in v2) — gated by `[Trait("Category", "PlatformIntegration")]`:
  - `WindowsInputSimulator.SendInput` returns success codes.
  - `WindowsIdleMonitor` returns plausible `TimeSpan` for known idle states.
- **Manual smoke test plan** (documented in repo `docs/manual-smoke.md`):
  - Teams status remains Available across 15 minutes of real idle.
  - Display turns off and stays off (with brief blink) when S1 ON.
  - Hotkey toggles pause state from any focused app.
  - Autostart entry survives reboot.
- **No automated UI tests in v1** — manual smoke is sufficient at this scale.

## 12. Build, Publish, Distribution

- **CI**: GitHub Actions builds and tests on `windows-latest` (and `macos-latest` in v2).
- **Publish**: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
- **Signing**: code-sign the binary if an Authenticode cert is available; otherwise ship unsigned (user accepts SmartScreen prompt on first run).
- **Distribution**: portable `.exe` for v1. Autostart is opt-in via Settings; no installer required.

## 13. v1 Acceptance Criteria

- App launches, tray icon appears, settings window opens, config persists across restart.
- Default config (60 s interval, Invisible mouse, F15 every 3rd cycle, S1 OFF, S3 OFF) keeps Teams "Available" across 15 minutes of real idle on a Windows 11 laptop.
- Idle-anchored: typing real keystrokes resets the countdown; no injection occurs until the system has been idle for a full interval.
- S1 toggle ON: display goes black within ~1 s of each injection tick; brief flash visible.
- S3 toggle ON: tray icon turns red, no synthetic input occurs, system does not sleep.
- Global hotkey (when enabled) toggles pause from any focused app.
- Autostart toggle creates/removes the `HKCU\...\Run` entry; app starts minimized after reboot.
- Working-hours schedule: outside the window, no injection occurs (the tool stays Running but the engine is gated); inside, normal behavior.

## 14. v2 (macOS) — Implementation Notes

- **Input** — `CGEventCreateMouseEvent` (no-delta or ±N px) and `CGEventCreateKeyboardEvent` for F13/F14/F15 (keycodes 105/107/113); post via `CGEventPost(kCGHIDEventTap, evt)`.
- **Idle** — `CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateHIDSystemState, kCGAnyInputEventType)`.
- **Power** — `IOPMAssertionCreateWithName(kIOPMAssertPreventUserIdleSystemSleep, ...)` and `IOPMAssertionRelease(...)`.
- **Display off** — invoke `pmset displaysleepnow` via `Process.Start` (or its underlying IOKit call).
- **Autostart** — `SMAppService` (modern, macOS 13+) or `LaunchAgents` plist for older systems.
- **Global hotkey** — Carbon `RegisterEventHotKey`.
- **Accessibility prompt** — `AXIsProcessTrustedWithOptions` to detect; guided UI to System Settings.

The macOS port implements the same Core interfaces listed in §5.1 (`IInputSimulator` with the `MoveMouse(MouseMode, int jigglePixels)` signature, `IPowerManager`, `IIdleMonitor`, `ISessionInfo`, `IAutoStartManager`, `IGlobalHotkeyService` incl. its `RegistrationResult` event) plus `IClock`. `ISessionInfo.IsRemoteSession` on macOS can be derived from a Screen Sharing / remote-login check (or simply return `false` initially — the only consumer is the RDP tooltip note). The `EnginePump` + `Scheduler` + `ActivityEngine` flow is platform-agnostic and is reused unchanged.
