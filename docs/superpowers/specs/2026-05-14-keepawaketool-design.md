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

| Mode | Behavior | Display |
|---|---|---|
| **Normal** (default) | Inject input each tick; system stays awake | Display follows the OS sleep policy |
| **S1: Force display off after injection** (toggle, default OFF) | Inject input, then ~200 ms later force display off | Brief "blink" each tick; display off otherwise |
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
    void MoveMouse(MouseMode mode);      // Invisible (0,0 delta) or Jiggle (±N px round-trip)
    void SendKey(VirtualKey key);        // F13 / F14 / F15
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
}
```

The DI container selects the Windows or macOS implementation at startup via `RuntimeInformation.IsOSPlatform(...)`.

### 5.2 Core Components

- **`ActivityEngine`** — owns a `PeriodicTimer(IntervalSeconds)`. Runs the per-tick decision tree (Section 6).
- **`PowerModeController`** — wires S1 and S3 toggles into `IPowerManager` state and gates `ActivityEngine`.
- **`ConfigurationStore`** — atomic read/write of `config.json`, `FileSystemWatcher`-based reactive change events.
- **`Scheduler`** — composes `ActivityEngine` + working-hours window + hotkey-pause flag into a unified Running/Paused/PowerSave state.

### 5.3 Platform.Win Implementations

- **`WindowsInputSimulator`** — `SendInput` with `INPUT_MOUSE` (delta `(0,0)` for Invisible; `(+1,0)` then `(-1,0)` for Jiggle) and `INPUT_KEYBOARD` (KEYDOWN + KEYUP pair).
- **`WindowsPowerManager`** — `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` for awake; `PostMessageW(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 2)` for display off.
- **`WindowsIdleMonitor`** — `GetLastInputInfo` + tick-count delta.
- **`WindowsAutoStartManager`** — registry value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- **`WindowsGlobalHotkeyService`** — `RegisterHotKey` against a hidden message-only window; message pump handles `WM_HOTKEY`.

### 5.4 App (Avalonia)

- **`App.axaml`** — DI setup, OS detection, single-instance mutex (`Global\KeepAwakeTool`).
- **`TrayIconController`** — Avalonia `TrayIcon`; state-driven color, live tooltip, context menu.
- **`SettingsWindow`** — five tabs: General / Activity / Power / Schedule / Hotkey.
- **`FirstRunPermissions`** (macOS, v2) — guided Accessibility-permission flow.

## 6. Data Flow — Single Tick

`ActivityEngine` tick (every `IntervalSeconds`):

```
tick
 ├─ S3 (Power-Save) ON?              ──► return
 ├─ Schedule enabled && out of hours?──► return
 ├─ HotkeyPaused?                    ──► return
 ├─ IdleMonitor < IdleThreshold?     ──► return (smart pause)
 ├─ InputSimulator.MoveMouse(config.MouseMode)
 ├─ if KeystrokeEnabled && (tickCount % EveryN == 0)
 │     InputSimulator.SendKey(config.Key)
 ├─ if ForceDisplayOff (S1)
 │     await Task.Delay(200ms); PowerManager.ForceDisplayOff()
 └─ TrayIcon.FlashHeartbeat()        (if enabled in config)
```

**Atomicity**: each tick awaits fully before the next is allowed. Config-change events cancel the current `PeriodicTimer` and restart with the new interval.

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

Transitions: tray menu commands, global hotkey (Running ↔ Paused), S3 toggle (Running ↔ PowerSave), schedule boundary crossings (when `schedule.enabled`: at `startTime` Stopped → Running on configured days; at `endTime` Running → Stopped).

## 8. Configuration

**Locations**:
- Windows: `%AppData%\KeepAwakeTool\config.json`
- macOS: `~/Library/Application Support/KeepAwakeTool/config.json`

**Schema (v1)**:

```jsonc
{
  "schemaVersion": 1,
  "activity": {
    "intervalSeconds": 60,              // range 10..240
    "idleThresholdSeconds": 30,         // smart-pause threshold (5..120)
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

- **Icon state**: gray (Stopped), green (Running), yellow (Paused), red (PowerSave/S3).
- **Tooltip**: `"KeepAwakeTool — Running (next tick in 47s)"`, live-updating.
- **Left-click**: open Settings window (or focus existing).
- **Right-click menu**:

```
● Status: Running                    (header)
─────────────
⏸ Pause / ▶ Resume
─────────────
⚡ Power-Save Mode (S3)              (checkable)
🌙 Force Display Off (S1)            (checkable)
─────────────
⚙ Settings…
─────────────
▶ Start with Windows                 (checkable)
─────────────
ⓘ About
✕ Quit
```

### 9.2 Settings Window

Fixed size ~600×450, five tabs:

1. **General** — interval slider (10s–4min), idle threshold (5s–120s), startup options, theme.
2. **Activity** — mouse mode radio (Invisible / Jiggle); jiggle pixel count (visible only when Jiggle); keystroke block (enabled, key dropdown F13/F14/F15, every-Nth cycle numeric).
3. **Power** — two toggles with short explainers: "Force display off after each injection (S1)" and "Power-Save Mode (S3) — disables presence injection".
4. **Schedule** — enabled checkbox; time-range picker; weekday checkboxes (visible only when enabled).
5. **Hotkey** — enabled checkbox; key-capture field; conflict validation message.

Footer buttons: `[Apply]` `[OK]` `[Cancel]`. Apply writes config and stays open.

### 9.3 First-Run (macOS only, v2)

If Accessibility permission missing, modal explains why the app needs it, a button opens **System Settings → Privacy & Security → Accessibility**, and a "Re-check" button reloads the permission state.

### 9.4 Single-Instance Enforcement

- Windows: named `Mutex` `Global\KeepAwakeTool`.
- macOS: lock file in `~/Library/Application Support/KeepAwakeTool/`.
- Second launch signals the existing instance to focus its Settings window via a named pipe (Windows) / Unix socket (macOS).

## 10. Error Handling & Edge Cases

### 10.1 Error Handling Principles

- **Native API failures** (`SendInput` returns 0, `SetThreadExecutionState` returns 0, `RegisterHotKey` fails) — log and continue; next tick retries.
- **Hotkey registration failure** — surface a one-time UI toast; user can pick a different combo.
- **Config file corrupt/unreadable** — write a `.corrupt.<timestamp>.json` backup, write defaults, continue, toast the user.
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
  - Smart-pause skip when `TimeSinceLastUserInput < IdleThreshold`.
  - S3 ON ⇒ no injection.
  - Working-hours window respected.
  - Hotkey-paused state respected.
  - Keystroke fires only every Nth tick.
  - S1 ⇒ `ForceDisplayOff` called ~200 ms after injection.
  - Config-change events restart timer with new interval.
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
- Smart-pause: typing real keystrokes for 5 seconds causes engine to skip the next tick.
- S1 toggle ON: display goes black within ~1 s of each injection tick; brief flash visible.
- S3 toggle ON: tray icon turns red, no synthetic input occurs, system does not sleep.
- Global hotkey (when enabled) toggles pause from any focused app.
- Autostart toggle creates/removes the `HKCU\...\Run` entry; app starts minimized after reboot.
- Working-hours schedule: outside the window, no injection occurs; inside, normal behavior.

## 14. v2 (macOS) — Implementation Notes

- **Input** — `CGEventCreateMouseEvent` (no-delta or ±N px) and `CGEventCreateKeyboardEvent` for F13/F14/F15 (keycodes 105/107/113); post via `CGEventPost(kCGHIDEventTap, evt)`.
- **Idle** — `CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateHIDSystemState, kCGAnyInputEventType)`.
- **Power** — `IOPMAssertionCreateWithName(kIOPMAssertPreventUserIdleSystemSleep, ...)` and `IOPMAssertionRelease(...)`.
- **Display off** — invoke `pmset displaysleepnow` via `Process.Start` (or its underlying IOKit call).
- **Autostart** — `SMAppService` (modern, macOS 13+) or `LaunchAgents` plist for older systems.
- **Global hotkey** — Carbon `RegisterEventHotKey`.
- **Accessibility prompt** — `AXIsProcessTrustedWithOptions` to detect; guided UI to System Settings.
