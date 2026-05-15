using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class GeneralTabViewModel
{
    public int IntervalSeconds { get; set; }
    public int IdleThresholdSeconds { get; set; }
    public bool AutoStartOnLogin { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public string Theme { get; set; } = "System";

    public GeneralTabViewModel(AppConfig cfg)
    {
        IntervalSeconds = cfg.Activity.IntervalSeconds;
        IdleThresholdSeconds = cfg.Activity.IdleThresholdSeconds;
        AutoStartOnLogin = cfg.Startup.AutoStartOnLogin;
        StartMinimizedToTray = cfg.Startup.StartMinimizedToTray;
        Theme = cfg.Ui.Theme;
    }

    public StartupConfig BuildStartup() => new()
    {
        AutoStartOnLogin = AutoStartOnLogin,
        StartMinimizedToTray = StartMinimizedToTray
    };

    public UiConfig BuildUi() => new() { Theme = Theme };
}
