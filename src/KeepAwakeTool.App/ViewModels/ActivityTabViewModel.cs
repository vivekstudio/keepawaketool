using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.App.ViewModels;

public sealed class ActivityTabViewModel : ObservableObject
{
    public MouseMode MouseMode { get; set; }
    public int JigglePixels { get; set; }
    private bool _keystrokeEnabled;
    public bool KeystrokeEnabled { get => _keystrokeEnabled; set => SetField(ref _keystrokeEnabled, value); }
    public VirtualKey KeystrokeKey { get; set; }
    public int EveryNthCycle { get; set; }

    public MouseMode[] MouseModes { get; } = { MouseMode.Invisible, MouseMode.Jiggle };
    public VirtualKey[] Keys { get; } = { VirtualKey.F13, VirtualKey.F14, VirtualKey.F15 };

    public decimal? JigglePixelsValue
    {
        get => JigglePixels;
        set => JigglePixels = (int)(value ?? 1);
    }

    public decimal? EveryNthCycleValue
    {
        get => EveryNthCycle;
        set => EveryNthCycle = (int)(value ?? 1);
    }

    public ActivityTabViewModel(AppConfig cfg)
    {
        MouseMode = cfg.Activity.Mouse.Mode;
        JigglePixels = cfg.Activity.Mouse.JigglePixels;
        KeystrokeEnabled = cfg.Activity.Keystroke.Enabled;
        KeystrokeKey = cfg.Activity.Keystroke.Key;
        EveryNthCycle = cfg.Activity.Keystroke.EveryNthCycle;
    }

    // Note: IntervalSeconds is owned by the General tab and
    // merged into the Activity section by SettingsViewModel.BuildConfig().
    public ActivityConfig Build() => new()
    {
        Mouse = new MouseConfig { Mode = MouseMode, JigglePixels = JigglePixels },
        Keystroke = new KeystrokeConfig { Enabled = KeystrokeEnabled, Key = KeystrokeKey, EveryNthCycle = EveryNthCycle }
    };
}
