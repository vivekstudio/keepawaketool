using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Diagnostics;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Core.Scheduling;
using KeepAwakeTool.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App.Tray;

public sealed class TrayIconController
{
    private readonly IServiceProvider _sp;
    private TrayIcon? _tray;
    private SettingsWindow? _settingsWindow;
    private NativeMenuItem? _pauseItem;
    private NativeMenuItem? _s1Item;
    private NativeMenuItem? _s3Item;
    private bool _remote;

    public TrayIconController(IServiceProvider sp) => _sp = sp;

    public void Initialize()
    {
        var scheduler = _sp.GetRequiredService<Scheduler>();
        scheduler.StateChanged += (_, state) => Dispatcher.UIThread.Post(() => UpdateIcon(state));

        _remote = _sp.GetRequiredService<ISessionInfo>().IsRemoteSession;
        if (_remote)
        {
            var logger = _sp.GetRequiredService<FileLogger>();
            logger.Log("INFO", "Remote session detected — S1 display-off is not meaningful over RDP");
        }

        _tray = new TrayIcon
        {
            ToolTipText = "KeepAwakeTool — Stopped",
            IsVisible = true,
            Menu = BuildMenu()
        };
        _tray.Clicked += (_, _) => Dispatcher.UIThread.Post(OpenSettings);
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _tray });
        SyncPowerChecks(_sp.GetRequiredService<Func<AppConfig>>()());
        _sp.GetRequiredService<ConfigurationStore>().Changed +=
            (_, cfg) => Dispatcher.UIThread.Post(() => SyncPowerChecks(cfg));
        UpdateIcon(scheduler.State);
        scheduler.Start();
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();
        _pauseItem = new NativeMenuItem("Pause");
        _pauseItem.Click += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _sp.GetRequiredService<Scheduler>().TogglePause());
        menu.Add(_pauseItem);
        menu.Add(new NativeMenuItemSeparator());
        _s1Item = new NativeMenuItem("Force display off (S1)") { ToggleType = MenuItemToggleType.CheckBox };
        _s1Item.Click += (_, _) => Dispatcher.UIThread.Post(() => TogglePower(s1: true));
        menu.Add(_s1Item);
        _s3Item = new NativeMenuItem("Power-Save Mode (S3)") { ToggleType = MenuItemToggleType.CheckBox };
        _s3Item.Click += (_, _) => Dispatcher.UIThread.Post(() => TogglePower(s1: false));
        menu.Add(_s3Item);
        menu.Add(new NativeMenuItemSeparator());
        var settings = new NativeMenuItem("Settings…");
        settings.Click += (_, _) => OpenSettings();
        menu.Add(settings);
        menu.Add(new NativeMenuItemSeparator());
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => { (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown(); };
        menu.Add(quit);
        return menu;
    }

    public void ShowSettings() => OpenSettings();

    private void OpenSettings()
    {
        if (_settingsWindow is null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new SettingsWindow(_sp);
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void TogglePower(bool s1)
    {
        var store = _sp.GetRequiredService<ConfigurationStore>();
        var cfg = store.Load();
        var power = s1
            ? cfg.Power with { ForceDisplayOffAfterInjection = !cfg.Power.ForceDisplayOffAfterInjection }
            : cfg.Power with { PowerSaveMode = !cfg.Power.PowerSaveMode };
        store.Save(cfg with { Power = power });
        SyncPowerChecks(cfg with { Power = power });
    }

    private void SyncPowerChecks(AppConfig cfg)
    {
        if (_s1Item is not null) _s1Item.IsChecked = cfg.Power.ForceDisplayOffAfterInjection;
        if (_s3Item is not null) _s3Item.IsChecked = cfg.Power.PowerSaveMode;
    }

    private void UpdateIcon(EngineState state)
    {
        if (_tray is null) return;
        var asset = state switch
        {
            EngineState.Running   => "avares://KeepAwakeTool/Tray/Assets/icon-running.ico",
            EngineState.Paused    => "avares://KeepAwakeTool/Tray/Assets/icon-paused.ico",
            EngineState.PowerSave => "avares://KeepAwakeTool/Tray/Assets/icon-powersave.ico",
            _                     => "avares://KeepAwakeTool/Tray/Assets/icon-stopped.ico"
        };
        using var stream = AssetLoader.Open(new Uri(asset));
        _tray.Icon = new WindowIcon(stream);
        var suffix = _remote ? " — RDP: display-off limited" : "";
        _tray.ToolTipText = $"KeepAwakeTool — {state}{suffix}";
        if (_pauseItem is not null)
            _pauseItem.Header = state == EngineState.Paused ? "Resume" : "Pause";
    }
}
