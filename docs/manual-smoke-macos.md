# KeepAwakeTool — macOS Manual Smoke Test (v2)

Run on macOS 14+ (Apple Silicon) from a self-contained publish:
`dotnet publish src/KeepAwakeTool.App -c Release -f net10.0 -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish`

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

(Note: I added `-f net10.0` to the publish command since the App now multi-targets and `dotnet publish` requires an explicit framework on multi-target projects.)
