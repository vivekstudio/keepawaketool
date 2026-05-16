using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Activity;

public sealed class ActivityEngine
{
    private readonly IInputSimulator _input;
    private readonly IIdleMonitor _idle;
    private readonly IPowerManager _power;
    private readonly IClock _clock;
    private AppConfig _config;
    private long _tickCount;

    public ActivityEngine(IInputSimulator input, IIdleMonitor idle, IPowerManager power, IClock clock, AppConfig config)
    {
        _input = input; _idle = idle; _power = power; _clock = clock; _config = config;
    }

    public event Action? Injected;

    public AppConfig Config => _config;
    public long TickCount => _tickCount;
    public bool HotkeyPaused { get; set; }
    public bool WithinWorkingHours { get; set; } = true;

    public void UpdateConfig(AppConfig newConfig) => _config = newConfig;

    public async Task TickAsync(CancellationToken ct)
    {
        _tickCount++;

        if (_config.Power.PowerSaveMode) return;
        if (!WithinWorkingHours) return;
        if (HotkeyPaused) return;
        if (_idle.TimeSinceLastUserInput() < TimeSpan.FromSeconds(_config.Activity.IdleThresholdSeconds)) return;

        _input.MoveMouse(_config.Activity.Mouse.Mode, _config.Activity.Mouse.JigglePixels);
        Injected?.Invoke();

        if (_config.Activity.Keystroke.Enabled && (_tickCount % _config.Activity.Keystroke.EveryNthCycle == 0))
            _input.SendKey(_config.Activity.Keystroke.Key);

        if (_config.Power.ForceDisplayOffAfterInjection)
        {
            await _clock.DelayAsync(TimeSpan.FromMilliseconds(200), ct);
            _power.ForceDisplayOff();
        }
    }
}
