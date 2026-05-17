# KeepAwakeTool — Manual Smoke Test Plan (Windows v1)

Run after each release build (`dotnet publish -c Release -r win-x64 --self-contained`).

## Pre-flight
- Fresh log in to a Windows 11 box with Microsoft Teams installed.
- No prior KeepAwakeTool config in `%AppData%\KeepAwakeTool\`.
- **Required: pin the tray icon to the always-visible system tray.** After first launch,
  go to Settings → Personalization → Taskbar → "Other system tray icons" and toggle
  **KeepAwakeTool** to On. If the icon is left in the Windows 11 "hidden icons" overflow
  flyout, the right-click context menu can hang/orphan when hovering items: the overflow
  flyout auto-closes on focus loss and Avalonia's `TrayIcon`/`NativeMenu` does not keep it
  alive or dismiss correctly (a framework-level limitation in `Avalonia.Win32`, not app
  code). Pinning the icon avoids the focus-fragile flyout and the menu behaves normally.

## 1. First run / defaults
1. Launch the published `KeepAwakeTool.exe`.
2. Expect: tray icon appears, color = green (Running).
3. Expect: `%AppData%\KeepAwakeTool\config.json` exists with defaults.
4. Left-click the tray icon — the Settings window opens (left-click again / re-open focuses the existing window, no duplicate).
5. Right-click the tray icon — menu shows: Pause, ─, Force display off (S1) [checkbox], Power-Save Mode (S3) [checkbox], ─, Settings…, ─, Quit. (No status header, no About, no "Start with Windows" — autostart is on the General tab.)

## 2. Presence with default config
1. Open Teams; sign in.
2. Do not touch keyboard/mouse for 15 minutes.
3. Expect: Teams status stays "Available" throughout.

## 3. Idle-anchored countdown
1. Open Settings → General; note the keep-alive interval (default 60 s).
2. Move the mouse; observe the tray tooltip — it should show a countdown close to the full interval.
3. Stop all input and wait.
4. Expect: the tooltip countdown decrements only while you are idle; at zero a synthetic input fires and the countdown resets to the full interval.
5. Move the mouse again; expect the countdown immediately resets toward the full interval and no injection occurs until idle ≥ interval.

## 4. S1 (Force display off) — settings and tray
1. Settings → Power → enable "Force display off after each injection". Apply.
2. Right-click the tray icon — the **Force display off (S1)** menu item now shows a checkmark (the tray checkbox reflects config and vice-versa: toggling it from the menu updates Settings).
3. After idle ≥ one interval an injection fires; ~200 ms later the display turns off. Expect a brief blink on each injection.

## 5. S3 (Power-Save Mode)
1. Settings → Power → enable "Power-Save Mode" (or toggle **Power-Save Mode (S3)** from the tray menu — it is checkable).
2. Apply.
3. Tray icon turns red. No mouse/keyboard input is injected. System still does not sleep (verify by waiting beyond the OS power-plan sleep timer).
4. Teams will mark you Away after its normal threshold. This is expected.

## 6. Global hotkey (key capture)
1. Settings → Hotkey → check "Enable global hotkey".
2. Click **Record…**; the button shows "Press keys…". Press and hold Ctrl+Alt then tap **P**; release — the captured combination "Ctrl+Alt+P" is shown next to the button. (You do not type the combo; it is captured from real key presses. Pressing **Esc** while recording cancels and restores the previous combo. Pressing a key with no modifier shows a validation hint.)
3. Apply.
4. From any focused app, press Ctrl+Alt+P. Tray icon turns yellow (Paused). Press again — returns to green.
5. Hotkey-registration-failure toast: set the hotkey to a combo already owned by Windows/another app (e.g. a Win+ shortcut the OS reserves), Apply, and expect a bottom-right toast: "Hotkey '…' could not be registered. It may already be in use…".

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

## 10. Theme
1. Settings → General → set Theme = Dark. Apply.
2. Expect: the Settings window (and any subsequent windows/toasts) switch to the dark variant immediately. Set back to Light, then System; each applies live without restart.

## 11. Heartbeat animation
1. Edit `%AppData%\KeepAwakeTool\config.json` and set `"ui": { "showHeartbeatAnimation": true }` (or via the relevant Settings control if present), save.
2. Stay idle through an injection.
3. Expect: on each injection the tray icon briefly flashes the heartbeat icon (~160 ms) then returns to the normal state icon. With the flag off (default), no flash.

## 12. RDP / remote session note
1. Connect to the box over Remote Desktop and launch (or already have) the app.
2. Hover the tray icon. Expect: the tooltip includes a note such as "— RDP: display-off limited" (injection still works; local display-off is not meaningful over RDP). A line is also written to the log.

## 13. Corrupt config recovery + toast
1. Quit the app. Replace `%AppData%\KeepAwakeTool\config.json` with invalid JSON (e.g. `{`).
2. Launch the app. Expect: a bottom-right toast stating config.json was unreadable and reset to defaults, naming a `config.corrupt.<timestamp>.json` backup; that backup file exists alongside a fresh default `config.json`; the app runs normally (tray icon green).
3. (Runtime variant) With the app running, overwrite `config.json` with invalid JSON and save. Expect: the same corrupt-quarantine toast appears via the file watcher.

## 14. Idle countdown tooltip
1. With the app Running, hover the tray icon.
2. Expect: tooltip reads like "KeepAwakeTool — Running — next activity in 47s" and the seconds decrement only while idle, resetting on real input (mirrors §3).

## 15. Crash safety
1. Force-kill `KeepAwakeTool.exe` via Task Manager.
2. Expect: no Windows sleep block remains; ES_CONTINUOUS clears.
3. Logs under `%AppData%\KeepAwakeTool\logs\keepawaketool-YYYYMMDD.log` are present and readable.
