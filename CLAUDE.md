# KeepAwakeTool — Project Guide (auto-loaded)

Background utility that keeps the OS active so presence apps (Teams, etc.) stay "Available",
with optional display-off (S1) and system-awake-only (S3) modes. **v1 = Windows (shipped,
in user smoke-test). v2 = macOS (not started).**

## Authoritative docs (read these, in order)

- **`docs/superpowers/specs/2026-05-14-keepawaketool-design.md`** — the source of truth.
  Reconciled to the shipped code. §14 = the macOS v2 implementation notes. Read it fully
  before any v2 work.
- `docs/manual-smoke.md` — manual test checklist for the real feature set.
- `docs/superpowers/plans/2026-05-14-keepawaketool.md` — **HISTORICAL only** (original v1
  plan; the code diverged a lot). Do not treat as current.

Note: Claude project *memory* is machine-local and does NOT travel with the clone. This
file + the spec are the only project knowledge a fresh agent gets.

## Architecture (1 screen)

- `KeepAwakeTool.Core` (`net10.0`) — platform-agnostic: `AppConfig`/`ConfigurationStore`,
  `ActivityEngine` (idle-gated, stateless re: timing), `Scheduler` (Stopped/Running/Paused/
  PowerSave), `PowerModeController`, `IClock`/`SystemClock`, and the platform **interfaces**.
- `KeepAwakeTool.Platform.Win` (`net10.0-windows`) — Win32 P/Invoke impls.
- `KeepAwakeTool.Platform.Mac` — **does not exist yet; this is v2.**
- `KeepAwakeTool.App` (`net10.0-windows`, Avalonia 12, WinExe) — `Program.cs`
  (`SingleInstanceGuard` mutex) → `App.axaml.cs` (DI via `Composition/ServiceRegistration`)
  → tray + 5-tab Settings + `EnginePump` (~1 s poll loop) + `ToastService`.
- Tests: `KeepAwakeTool.Core.Tests`, `KeepAwakeTool.App.Tests` (xUnit/NSubstitute/FluentAssertions).

**Timing model (important):** there is ONE knob `IntervalSeconds`. The engine injects only
when `IIdleMonitor.TimeSinceLastUserInput() >= IntervalSeconds`. Synthetic `SendInput`
also resets the OS idle timer, so cadence self-anchors to last real-or-synthetic input.
There is no separate "idle threshold". Keystroke fires every Nth *injection*.

## v2 (macOS) — where to start

`KeepAwakeTool.App` currently targets `net10.0-windows` and hard-references
`Platform.Win`. **v2 must restructure this**: a macOS-compatible App TFM with
OS-conditional platform project references, and DI selecting impls by
`RuntimeInformation.IsOSPlatform`. Then create `KeepAwakeTool.Platform.Mac`
implementing these Core interfaces (see spec §5.1 / §14 for exact APIs):

- `IInputSimulator` → CoreGraphics `CGEventCreateMouseEvent`/`CGEventCreateKeyboardEvent` + `CGEventPost`
- `IIdleMonitor` → `CGEventSourceSecondsSinceLastEventType`
- `IPowerManager` → IOKit `IOPMAssertionCreateWithName` (system-awake) + `pmset displaysleepnow`
- `IAutoStartManager` → `SMAppService` (macOS 13+) or LaunchAgents plist
- `IGlobalHotkeyService` → Carbon `RegisterEventHotKey` (or `NSEvent` global monitor)
- `ISessionInfo` → screen-sharing/remote detection (best-effort)
- `IClock`/`SystemClock` — already cross-platform, reuse as-is.
- First-run **Accessibility permission** prompt is required for synthetic input on macOS
  (`AXIsProcessTrustedWithOptions`) — there is no Windows equivalent. See spec §5.4/§14.

`ActivityEngine`, `Scheduler`, `PowerModeController`, `EnginePump`, all ViewModels, and
the Avalonia UI are already cross-platform and should be reused unchanged.

## Build / test / run

```
dotnet build -c Release
dotnet test  -c Release -- xUnit.ParallelizeTestCollections=false
# Windows publish:
dotnet publish src/KeepAwakeTool.App -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o publish
# macOS (Apple Silicon) publish (v2):
dotnet publish src/KeepAwakeTool.App -c Release -r osx-arm64  --self-contained -p:PublishSingleFile=true -o publish
```

Always run tests with `-- xUnit.ParallelizeTestCollections=false`:
`Changed_event_fires_when_file_is_modified` is a `FileSystemWatcher`-timing test that
flakes only under parallel collections (passes reliably non-parallel). Disabling parallel
collections / isolating that test is the one open pre-PR/CI cleanup item.

## Git

- Remote: `https://github.com/vivekstudio/keepawaketool.git` (**private**). Working branch:
  `feature/v1-implementation`.
- **Identity (set LOCALLY on every clone — never `--global`):**
  `git config user.name "vivekstudio"` and `git config user.email "vkc9191@gmail.com"`.
  The owner has multiple GitHub accounts; this repo must commit as `vivekstudio`.
- Conventional commits (`feat:`/`fix:`/`docs:`/`refactor:`/`chore:`), small frequent
  commits, co-author trailer for AI commits.

## Constraints / gotchas

- .NET 10 SDK + Avalonia 12 required. App is single-file self-contained (~85 MB).
- FluentAssertions is v8 — **commercially licensed**, but test-only (never shipped in the
  binary). Pin to 7.x if a strict-MIT test stack is ever required.
- Windows-only quirk (irrelevant on macOS but don't "fix" it cross-platform): the tray
  `NativeMenu` can hang when the icon is in the Win11 "hidden icons" overflow flyout;
  documented workaround = pin the icon. Avalonia framework limitation.

## Workflow

Use the superpowers skills: brainstorm → spec → plan → subagent-driven execution with
two-stage review (spec compliance, then code quality), verification-before-completion
(build + tests green, evidence before claims), systematic-debugging (root cause before
fixes — the Iron Law). For v2: brainstorm the macOS restructure first, write a v2 spec
under `docs/superpowers/specs/`, then a plan, then execute.
