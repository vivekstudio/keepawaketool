using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using KeepAwakeTool.Core.Activity;
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

    public TrayIconController(IServiceProvider sp) => _sp = sp;

    public void Initialize()
    {
        var scheduler = _sp.GetRequiredService<Scheduler>();
        scheduler.StateChanged += (_, state) => Dispatcher.UIThread.Post(() => UpdateIcon(state));

        _tray = new TrayIcon
        {
            ToolTipText = "KeepAwakeTool — Stopped",
            IsVisible = true,
            Menu = BuildMenu()
        };
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _tray });
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
        _tray.ToolTipText = $"KeepAwakeTool — {state}";
        if (_pauseItem is not null)
            _pauseItem.Header = state == EngineState.Paused ? "Resume" : "Pause";
    }
}
