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
