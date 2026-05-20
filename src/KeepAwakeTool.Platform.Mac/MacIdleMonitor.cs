using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacIdleMonitor : IIdleMonitor
{
    public TimeSpan TimeSinceLastUserInput()
    {
        var seconds = CoreGraphics.CGEventSourceSecondsSinceLastEventType(
            CoreGraphics.kCGEventSourceStateHIDSystemState,
            CoreGraphics.kCGAnyInputEventType);
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        return TimeSpan.FromSeconds(seconds);
    }
}
