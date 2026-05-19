using System;
using Microsoft.Win32;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsSystemPowerEvents : ISystemPowerEvents
{
    public event Action? Resumed;
    private bool _started;

    public void Start()
    {
        if (_started) return;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _started = false;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Resumed?.Invoke();
    }

    public void Dispose() => Stop();
}
