using System;
using System.Threading;
using System.Threading.Tasks;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.Core.Scheduling;

public sealed class Scheduler
{
    private readonly ActivityEngine _engine;
    private readonly Func<AppConfig> _configProvider;
    private readonly IClock _clock;

    public Scheduler(ActivityEngine engine, Func<AppConfig> configProvider, IClock clock)
    {
        _engine = engine; _configProvider = configProvider; _clock = clock;
    }

    public EngineState State { get; private set; } = EngineState.Stopped;
    public event EventHandler<EngineState>? StateChanged;

    public void Start()  => SetState(EvaluateState());
    public void Stop()   => SetState(EngineState.Stopped);
    public void TogglePause()
    {
        if (State == EngineState.Running) { _engine.HotkeyPaused = true;  SetState(EngineState.Paused); }
        else if (State == EngineState.Paused) { _engine.HotkeyPaused = false; SetState(EngineState.Running); }
    }

    public void ApplyConfig(AppConfig cfg)
    {
        _engine.UpdateConfig(cfg);
        if (State != EngineState.Stopped) SetState(EvaluateState());
    }

    public async Task RunOneTickAsync(CancellationToken ct)
    {
        var cfg = _configProvider();
        _engine.UpdateConfig(cfg);
        _engine.WithinWorkingHours = WorkingHoursPolicy.IsWithinWindow(cfg.Schedule, _clock.LocalNow);
        SetState(EvaluateState());
        if (State == EngineState.Running) await _engine.TickAsync(ct);
    }

    private EngineState EvaluateState()
    {
        var cfg = _configProvider();
        if (cfg.Power.PowerSaveMode) return EngineState.PowerSave;
        if (_engine.HotkeyPaused) return EngineState.Paused;
        return EngineState.Running;
    }

    private void SetState(EngineState next)
    {
        if (State == next) return;
        State = next;
        StateChanged?.Invoke(this, next);
    }
}
