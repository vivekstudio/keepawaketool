using System;

namespace KeepAwakeTool.Core.Platform;

/// <summary>
/// Raises <see cref="Resumed"/> when the machine wakes from sleep, so the
/// awake assertion can be re-armed. Windows: SystemEvents.PowerModeChanged.
/// macOS: IOKit IORegisterForSystemPower.
/// </summary>
public interface ISystemPowerEvents : IDisposable
{
    event Action? Resumed;
    void Start();
    void Stop();
}
