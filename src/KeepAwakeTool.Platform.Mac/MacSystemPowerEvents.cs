using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacSystemPowerEvents : ISystemPowerEvents
{
    public event Action? Resumed;

    private RunLoopThread? _loop;
    private IntPtr _rootPort;
    private IntPtr _notifier;
    private IntPtr _notifyPort;
    private IOKit.IOServiceInterestCallback? _callback; // kept alive against GC

    public void Start()
    {
        if (_loop is not null) return;
        _callback = OnPower;
        var cbPtr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_callback);

        _loop = new RunLoopThread("KAT-PowerEvents", () =>
        {
            _rootPort = IOKit.IORegisterForSystemPower(IntPtr.Zero, out _notifyPort, cbPtr, out _notifier);
            if (_notifyPort != IntPtr.Zero)
            {
                var src = IOKit.IONotificationPortGetRunLoopSource(_notifyPort);
                CoreFoundation.CFRunLoopAddSource(
                    CoreFoundation.CFRunLoopGetCurrent(), src, CoreFoundation.kCFRunLoopCommonModes);
            }
        });
        _loop.Start();
    }

    private void OnPower(IntPtr refcon, IntPtr service, uint messageType, IntPtr messageArgument)
    {
        // Acknowledge sleep/wake messages so the system is not blocked.
        IOKit.IOAllowPowerChange(_rootPort, messageArgument);
        if (messageType == IOKit.kIOMessageSystemHasPoweredOn) Resumed?.Invoke();
    }

    public void Stop()
    {
        if (_loop is null) return;
        if (_notifier != IntPtr.Zero) IOKit.IODeregisterForSystemPower(ref _notifier);
        _loop.Dispose();
        _loop = null;
        _callback = null;
    }

    public void Dispose() => Stop();
}
