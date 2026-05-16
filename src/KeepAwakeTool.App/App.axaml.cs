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

            _pump = new EnginePump(Services, Services.GetRequiredService<Core.Diagnostics.FileLogger>());
            _pump.Start();
            _log.Log("INFO", "Engine pump started");

            var store = Services.GetRequiredService<KeepAwakeTool.Core.Config.ConfigurationStore>();
            store.Changed += (_, cfg) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ApplyAutostart(cfg);
                ApplyHotkey(cfg);
                ApplyTheme(cfg);
            });

            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;

            ApplyAutostart(configProvider());
            ApplyHotkey(configProvider());

            desktop.Exit += (_, _) =>
            {
                _log?.Log("INFO", "Shutting down");
                Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                _pump?.Dispose();
                power.Stop();
                Services.GetRequiredService<IGlobalHotkeyService>().Unregister();
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
}
