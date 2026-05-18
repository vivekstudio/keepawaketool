using System;
using KeepAwakeTool.Core.Activity;

namespace KeepAwakeTool.App;

public static class StatusText
{
    /// Canonical live-status segment shared by the tray tooltip and the Settings banner.
    /// Example: "Running · S1 off · S3 on · next activity in 42s"  (countdown only when Running).
    public static string Build(EngineState state, bool s1, bool s3, int intervalSeconds, TimeSpan idle)
    {
        var s = $"{state} · S1 {(s1 ? "on" : "off")} · S3 {(s3 ? "on" : "off")}";
        if (state == EngineState.Running)
        {
            var secs = (int)Math.Max(0, intervalSeconds - idle.TotalSeconds);
            s += $" · next activity in {secs}s";
        }
        return s;
    }
}
