using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

/// <summary>
/// Login-item registration via SMAppService.mainApp (macOS 13+; baseline here is 14+).
/// SMAppServiceStatus: 0 = NotRegistered, 1 = Enabled, 2 = RequiresApproval, 3 = NotFound.
/// </summary>
public sealed class MacAutoStartManager : IAutoStartManager
{
    private readonly Action<string, string>? _log;
    public MacAutoStartManager(Action<string, string>? log = null) => _log = log;

    private static IntPtr MainAppService()
    {
        var cls = ObjC.objc_getClass("SMAppService");
        return cls == IntPtr.Zero ? IntPtr.Zero : ObjC.Send(cls, "mainApp");
    }

    public bool IsEnabled
    {
        get
        {
            var svc = MainAppService();
            if (svc == IntPtr.Zero) return false;
            return ObjC.SendLong(svc, "status") == 1; // SMAppServiceStatusEnabled
        }
    }

    public void Enable()
    {
        var svc = MainAppService();
        if (svc == IntPtr.Zero) { _log?.Invoke("ERROR", "SMAppService unavailable"); return; }
        ObjC.SendPtr(svc, "registerAndReturnError:", IntPtr.Zero);
    }

    public void Disable()
    {
        var svc = MainAppService();
        if (svc == IntPtr.Zero) { _log?.Invoke("ERROR", "SMAppService unavailable"); return; }
        ObjC.SendPtr(svc, "unregisterAndReturnError:", IntPtr.Zero);
    }
}
