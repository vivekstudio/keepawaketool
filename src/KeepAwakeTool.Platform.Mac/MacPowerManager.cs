using System;
using System.Diagnostics;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacPowerManager : IPowerManager
{
    private readonly Action<string, string>? _log;
    private uint _assertionId;
    private bool _asserted;

    public MacPowerManager(Action<string, string>? log = null) => _log = log;

    public void KeepSystemAwake(bool on)
    {
        if (on)
        {
            if (_asserted) return;
            // Both AssertionType and AssertionName are CFStringRef in IOPMLib.h
            // (despite the assertion-type constant being a plain C string macro).
            var type = CoreFoundation.CFStr(IOKit.kIOPMAssertPreventUserIdleSystemSleep);
            var name = CoreFoundation.CFStr("KeepAwakeTool");
            try
            {
                var rc = IOKit.IOPMAssertionCreateWithName(
                    type, IOKit.kIOPMAssertionLevelOn, name, out _assertionId);
                if (rc == 0) _asserted = true;
                else _log?.Invoke("ERROR", $"IOPMAssertionCreateWithName failed rc={rc}");
            }
            finally
            {
                if (name != IntPtr.Zero) CoreFoundation.CFRelease(name);
                if (type != IntPtr.Zero) CoreFoundation.CFRelease(type);
            }
        }
        else
        {
            if (!_asserted) return;
            var rc = IOKit.IOPMAssertionRelease(_assertionId);
            if (rc != 0) _log?.Invoke("ERROR", $"IOPMAssertionRelease failed rc={rc}");
            _asserted = false;
            _assertionId = 0;
        }
    }

    public void ForceDisplayOff()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("/usr/bin/pmset", "displaysleepnow")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            _log?.Invoke("ERROR", "pmset displaysleepnow failed: " + ex.Message);
        }
    }
}
