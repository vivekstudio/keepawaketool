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

        this.FindControl<Button>("OpenA11yButton")!.Click += (_, _) => OpenAccessibilitySettings();
        this.FindControl<Button>("RecheckA11yButton")!.Click += (_, _) => RefreshStatus();

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
        var border = this.FindControl<Border>("StatusBorder");
        var openBtn = this.FindControl<Button>("OpenA11yButton");
        var recheckBtn = this.FindControl<Button>("RecheckA11yButton");
        if (banner is null || border is null || openBtn is null || recheckBtn is null) return;

        var gate = _sp.GetRequiredService<IPermissionGate>();
        if (!gate.CanInjectInput)
        {
            banner.Text = "Accessibility permission required — KeepAwakeTool cannot keep you "
                        + "active until it is granted in System Settings ▸ Privacy & Security ▸ Accessibility.";
            border.Background = Avalonia.Media.Brushes.DarkOrange;
            openBtn.IsVisible = true;
            recheckBtn.IsVisible = true;
            return;
        }

        openBtn.IsVisible = false;
        recheckBtn.IsVisible = false;
        border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x22, 0x80, 0x80, 0x80));

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

    private void OpenAccessibilitySettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "open",
                "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
            { UseShellExecute = false });
        }
        catch { /* best-effort; banner stays until Re-check confirms grant */ }
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
