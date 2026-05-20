using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KeepAwakeTool.App.Composition;
using KeepAwakeTool.App.Tray;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Diagnostics;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Core.Power;
using KeepAwakeTool.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace KeepAwakeTool.App;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = default!;
    public TrayIconController? Tray { get; private set; }
    private EnginePump? _pump;
    private FileLogger? _log;

    // Idempotency guards: skip re-applies when the config section is unchanged.
    private HotkeyConfig? _lastHotkey;
    private StartupConfig? _lastStartup;
    private string? _lastTheme;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServiceRegistration.Build();
        _log = Services.GetRequiredService<FileLogger>();
        _log.Log("INFO", "App starting");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var configProvider = Services.GetRequiredService<Func<AppConfig>>();
            var power = Services.GetRequiredService<PowerModeController>();
            power.Start(configProvider().Power);

            Tray = new TrayIconController(Services);
            Tray.Initialize();
            ApplyTheme(configProvider());

            if (!configProvider().Startup.StartMinimizedToTray)
                Tray.ShowSettings();

            _pump = Services.GetRequiredService<EnginePump>();
            _pump.Start();
            _log.Log("INFO", "Engine pump started");

            var store = Services.GetRequiredService<KeepAwakeTool.Core.Config.ConfigurationStore>();
            store.Changed += (_, cfg) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ApplyAutostart(cfg);
                ApplyHotkey(cfg);
                ApplyTheme(cfg);
            });

#if WINDOWS
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
#endif

            ApplyAutostart(configProvider());
            ApplyHotkey(configProvider());

            // Toast notifications (§10.1): wire after UI is ready so Show() can access Screens.
            var toasts = Services.GetRequiredService<KeepAwakeTool.App.Notifications.ToastService>();

            // Hotkey-registration failure (raised from pump thread; ToastService self-marshals).
            Services.GetRequiredService<IGlobalHotkeyService>().RegistrationResult += ok =>
            {
                if (!ok)
                {
                    var combo = configProvider().Hotkey.Combination;
                    toasts.Show($"Hotkey '{combo}' could not be registered. It may already be in use by another application.");
                }
            };

            // Corrupt config — startup (LastCorruptBackupPath set before UI existed).
            if (store.LastCorruptBackupPath is { } startupBackup)
                toasts.Show($"config.json was unreadable and has been reset to defaults. A backup was saved as: {System.IO.Path.GetFileName(startupBackup)}");

            // Corrupt config — runtime (FSW reload while app is running).
            store.CorruptQuarantined += b =>
                toasts.Show($"config.json became unreadable and was reset to defaults. Backup: {System.IO.Path.GetFileName(b)}");

            desktop.Exit += (_, _) =>
            {
                _log?.Log("INFO", "Shutting down");
#if WINDOWS
                Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
#endif
                _pump?.Dispose();
                power.Stop();
                (Services.GetRequiredService<IGlobalHotkeyService>() as IDisposable)?.Dispose();
            };

            _log.Log("INFO", "Initialization complete");
        }
        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyTheme(AppConfig cfg)
    {
        if (_lastTheme == cfg.Ui.Theme) return;
        _lastTheme = cfg.Ui.Theme;

        RequestedThemeVariant = cfg.Ui.Theme switch
        {
            "Light" => Avalonia.Styling.ThemeVariant.Light,
            "Dark"  => Avalonia.Styling.ThemeVariant.Dark,
            _        => Avalonia.Styling.ThemeVariant.Default
        };
    }

    public void ApplyAutostart(AppConfig cfg)
    {
        if (_lastStartup == cfg.Startup) return;
        _lastStartup = cfg.Startup;

        var mgr = Services.GetRequiredService<IAutoStartManager>();
        if (cfg.Startup.AutoStartOnLogin && !mgr.IsEnabled) mgr.Enable();
        else if (!cfg.Startup.AutoStartOnLogin && mgr.IsEnabled) mgr.Disable();
    }

    public void ApplyHotkey(AppConfig cfg)
    {
        if (_lastHotkey == cfg.Hotkey) return;
        _lastHotkey = cfg.Hotkey;

        _log?.Log("INFO", $"ApplyHotkey enabled={cfg.Hotkey.Enabled} combo='{cfg.Hotkey.Combination}'");

        var hk = Services.GetRequiredService<IGlobalHotkeyService>();
        hk.Unregister();
        if (!cfg.Hotkey.Enabled) return;
        var scheduler = Services.GetRequiredService<Scheduler>();
        try
        {
            var hotkey = Hotkey.Parse(cfg.Hotkey.Combination);
            hk.TryRegister(hotkey, () => Dispatcher.UIThread.Post(scheduler.TogglePause));
        }
        catch (ArgumentException)
        {
            _log?.Log("ERROR", $"Invalid hotkey combo '{cfg.Hotkey.Combination}'");
        }
    }

#if WINDOWS
    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Services.GetRequiredService<Core.Power.PowerModeController>().Rearm();
            });
        }
    }
#endif
}
