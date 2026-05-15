using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KeepAwakeTool.App.Composition;
using KeepAwakeTool.App.Tray;
using KeepAwakeTool.Core.Config;
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

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ServiceRegistration.Build();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var configProvider = Services.GetRequiredService<Func<AppConfig>>();
            var power = Services.GetRequiredService<PowerModeController>();
            power.Start(configProvider().Power);

            Tray = new TrayIconController(Services);
            Tray.Initialize();

            _pump = new EnginePump(Services);
            _pump.Start();

            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;

            ApplyAutostart(configProvider());
            ApplyHotkey(configProvider());

            desktop.Exit += (_, _) =>
            {
                Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                _pump?.Dispose();
                power.Stop();
                Services.GetRequiredService<IGlobalHotkeyService>().Unregister();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyAutostart(AppConfig cfg)
    {
        var mgr = Services.GetRequiredService<IAutoStartManager>();
        if (cfg.Startup.AutoStartOnLogin && !mgr.IsEnabled) mgr.Enable();
        else if (!cfg.Startup.AutoStartOnLogin && mgr.IsEnabled) mgr.Disable();
    }

    public void ApplyHotkey(AppConfig cfg)
    {
        var hk = Services.GetRequiredService<IGlobalHotkeyService>();
        hk.Unregister();
        if (!cfg.Hotkey.Enabled) return;
        var scheduler = Services.GetRequiredService<Scheduler>();
        try
        {
            var hotkey = Hotkey.Parse(cfg.Hotkey.Combination);
            hk.TryRegister(hotkey, () => Dispatcher.UIThread.Post(scheduler.TogglePause));
        }
        catch (ArgumentException) { /* invalid combination — skip */ }
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var configProvider = Services.GetRequiredService<Func<Core.Config.AppConfig>>();
                Services.GetRequiredService<Core.Power.PowerModeController>().Start(configProvider().Power);
            });
        }
    }
}
