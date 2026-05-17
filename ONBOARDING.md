# KeepAwakeTool — Onboarding (new agent / new machine)

You're picking up **KeepAwakeTool** to start **v2 (macOS support)**. v1 (Windows) is built
and in user smoke-test. This guide gets you productive fast. `CLAUDE.md` (repo root) is
auto-loaded and has the durable details — this is the "first session" walkthrough.

## 0. Context that does NOT travel

Claude project memory is per-machine. On this MacBook you start with zero memory of the
project. Everything you need is in the repo: this file, `CLAUDE.md`, and the design spec.

## 1. First-session checklist

1. Clone & branch:
   ```
   git clone https://github.com/vivekstudio/keepawaketool.git
   cd keepawaketool
   git checkout feature/v1-implementation
   ```
2. **Set the git identity locally (required — owner has multiple GitHub accounts):**
   ```
   git config user.name  "vivekstudio"
   git config user.email "vkc9191@gmail.com"
   ```
   Never use `--global` for this repo.
3. Prereqs: install **.NET 10 SDK** (Avalonia 12 pulls via NuGet). Apple Silicon assumed.
4. Sanity build/test (Core + tests are cross-platform and will pass on macOS even before
   v2 work; the Windows platform project will not be exercised):
   ```
   dotnet build -c Release
   dotnet test  -c Release -- xUnit.ParallelizeTestCollections=false
   ```
   Always pass the non-parallel flag (one `FileSystemWatcher` test flakes under parallel
   collections only).
5. Read `docs/superpowers/specs/2026-05-14-keepawaketool-design.md` end to end —
   **especially §5.1 (interfaces), §6 (idle-anchored timing), §14 (macOS notes).**
   Treat `docs/superpowers/plans/...` as historical only.

## 2. What v2 actually is

The Avalonia UI, `ActivityEngine`, `Scheduler`, `PowerModeController`, `EnginePump`,
ViewModels, config — all cross-platform, reuse unchanged. v2 is two pieces of work:

1. **Restructure** `KeepAwakeTool.App` (currently `net10.0-windows`, hard-references
   `Platform.Win`) so it builds on macOS — OS-conditional TFM + platform project
   references, DI selecting impls via `RuntimeInformation.IsOSPlatform`.
2. **Create `KeepAwakeTool.Platform.Mac`** implementing the Core interfaces
   (`IInputSimulator`, `IIdleMonitor`, `IPowerManager`, `IAutoStartManager`,
   `IGlobalHotkeyService`, `ISessionInfo`) with the macOS APIs listed in `CLAUDE.md` /
   spec §14, plus the first-run **Accessibility permission** flow (no Windows analog).

## 3. How to work here

Follow the superpowers workflow that built v1:
- **brainstorm** the macOS restructure → write a **v2 spec** in
  `docs/superpowers/specs/` → **plan** → **subagent-driven execution** with the two-stage
  review (spec compliance, then code quality) per task.
- **systematic-debugging** Iron Law: root cause before any fix.
- **verification-before-completion**: build + tests green and evidence before claiming done.
- Conventional commits; small, frequent; AI co-author trailer.
- You can actually run the macOS build on this machine — use that: publish with
  `dotnet publish src/KeepAwakeTool.App -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish`
  and have the human smoke-test the real app (that's why v2 moved to the Mac).

## 4. Gotchas

- FluentAssertions v8 is commercially licensed but test-only (not shipped).
- Don't "fix" the Windows-only Win11 tray-overflow menu quirk on macOS — it's a Windows
  Avalonia limitation, documented, irrelevant to Mac.
- The remote may be ahead of any local memory you imagine — trust the repo + spec + git
  history, not assumptions.

Welcome aboard — start by reading the spec, then brainstorm the v2 restructure with the human.
