using System.Linq;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Config;

public sealed record AppConfig
{
    public int SchemaVersion { get; init; } = 1;
    public ActivityConfig Activity { get; init; } = new();
    public PowerConfig Power { get; init; } = new();
    public ScheduleConfig Schedule { get; init; } = new();
    public HotkeyConfig Hotkey { get; init; } = new();
    public StartupConfig Startup { get; init; } = new();
    public UiConfig Ui { get; init; } = new();
}

public sealed record ActivityConfig
{
    public int IntervalSeconds { get; init; } = 60;
    public MouseConfig Mouse { get; init; } = new();
    public KeystrokeConfig Keystroke { get; init; } = new();
}

public sealed record MouseConfig
{
    public MouseMode Mode { get; init; } = MouseMode.Invisible;
    public int JigglePixels { get; init; } = 1;
}

public sealed record KeystrokeConfig
{
    public bool Enabled { get; init; } = true;
    public VirtualKey Key { get; init; } = VirtualKey.F15;
    public int EveryNthCycle { get; init; } = 3;
}

public sealed record PowerConfig
{
    public bool ForceDisplayOffAfterInjection { get; init; } = false;
    public bool PowerSaveMode { get; init; } = false;
}

public sealed record ScheduleConfig
{
    public bool Enabled { get; init; } = false;
    public string StartTime { get; init; } = "09:00";
    public string EndTime { get; init; } = "18:00";
    public IReadOnlyList<DayOfWeek> Days { get; init; } = new[]
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    };

    public bool Equals(ScheduleConfig? other) =>
        other is not null
        && Enabled == other.Enabled
        && StartTime == other.StartTime
        && EndTime == other.EndTime
        && Days.SequenceEqual(other.Days);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Enabled);
        h.Add(StartTime);
        h.Add(EndTime);
        foreach (var d in Days) h.Add(d);
        return h.ToHashCode();
    }
}

public sealed record HotkeyConfig
{
    public bool Enabled { get; init; } = false;
    public string Combination { get; init; } = "Ctrl+Alt+P";
}

public sealed record StartupConfig
{
    public bool AutoStartOnLogin { get; init; } = false;
    public bool StartMinimizedToTray { get; init; } = true;
}

public sealed record UiConfig
{
    public string Theme { get; init; } = "System";
    public bool ShowHeartbeatAnimation { get; init; } = false;
}
