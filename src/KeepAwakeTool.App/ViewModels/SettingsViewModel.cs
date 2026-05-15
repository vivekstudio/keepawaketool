using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class SettingsViewModel
{
    private readonly AppConfig _draft;

    public GeneralTabViewModel General { get; }
    public ActivityTabViewModel Activity { get; }
    public PowerTabViewModel Power { get; }
    public ScheduleTabViewModel Schedule { get; }
    public HotkeyTabViewModel Hotkey { get; }

    public SettingsViewModel(AppConfig initial)
    {
        _draft = initial;
        General  = new GeneralTabViewModel(initial);
        Activity = new ActivityTabViewModel(initial);
        Power    = new PowerTabViewModel(initial);
        Schedule = new ScheduleTabViewModel(initial);
        Hotkey   = new HotkeyTabViewModel(initial);
    }

    public AppConfig BuildConfig() => _draft with
    {
        Activity = Activity.Build() with
        {
            IntervalSeconds = General.IntervalSeconds,
            IdleThresholdSeconds = General.IdleThresholdSeconds
        },
        Power    = Power.Build(),
        Schedule = Schedule.Build(),
        Hotkey   = Hotkey.Build(),
        Startup  = General.BuildStartup(),
        Ui       = General.BuildUi()
    };
}
