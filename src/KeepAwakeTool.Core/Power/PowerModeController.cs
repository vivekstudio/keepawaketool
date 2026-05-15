using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Power;

public sealed class PowerModeController
{
    private readonly IPowerManager _power;
    private bool _awake;

    public PowerModeController(IPowerManager power) => _power = power;

    public void Start(PowerConfig _)
    {
        if (_awake) return;
        _power.KeepSystemAwake(true);
        _awake = true;
    }

    public void Rearm()
    {
        if (_awake) _power.KeepSystemAwake(true);
    }

    public void ApplyConfig(PowerConfig _) { /* no-op for now; reserved for future per-config logic */ }

    public void Stop()
    {
        if (!_awake) return;
        _power.KeepSystemAwake(false);
        _awake = false;
    }
}
