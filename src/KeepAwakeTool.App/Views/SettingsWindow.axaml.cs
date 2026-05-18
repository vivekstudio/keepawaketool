using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KeepAwakeTool.App.ViewModels;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App.Views;

public partial class SettingsWindow : Window
{
    private readonly IServiceProvider _sp;
    private readonly ConfigurationStore _store;
    private readonly SettingsViewModel _vm;
    private DispatcherTimer? _statusTimer;

    public SettingsWindow(IServiceProvider sp)
    {
        _sp = sp;
        _store = sp.GetRequiredService<ConfigurationStore>();
        _vm = new SettingsViewModel(_store.Load());
        InitializeComponent();
        DataContext = _vm;

        this.FindControl<Button>("ApplyButton")!.Click += (_, _) => ApplyAndStay();
        this.FindControl<Button>("OkButton")!.Click    += (_, _) => { ApplyAndStay(); Close(); };
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
        RefreshStatus();
        Closed += (_, _) => _statusTimer?.Stop();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RefreshStatus()
    {
        var banner = this.FindControl<TextBlock>("StatusBanner");
        if (banner is null) return;
        var scheduler = _sp.GetRequiredService<Scheduler>();
        var cfg = _sp.GetRequiredService<Func<AppConfig>>()();
        var idle = _sp.GetRequiredService<IIdleMonitor>().TimeSinceLastUserInput();
        banner.Text = "Status: " + StatusText.Build(
            scheduler.State,
            cfg.Power.ForceDisplayOffAfterInjection,
            cfg.Power.PowerSaveMode,
            cfg.Activity.IntervalSeconds,
            idle);
    }

    private void ApplyAndStay()
    {
        var cfg = _vm.BuildConfig();
        _store.Save(cfg);

        if (Avalonia.Application.Current is KeepAwakeTool.App.App app)
        {
            app.ApplyAutostart(cfg);
            app.ApplyHotkey(cfg);
            app.ApplyTheme(cfg);
        }
    }
}
