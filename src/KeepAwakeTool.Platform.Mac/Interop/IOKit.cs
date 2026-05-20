using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class IOKit
{
    private const string Lib = "/System/Library/Frameworks/IOKit.framework/IOKit";

    internal const uint kIOPMAssertionLevelOn = 255;
    // Message type delivered to the IOServiceInterestCallback on wake.
    internal const uint kIOMessageSystemHasPoweredOn = 0xE0000300;

    // BOTH assertionType AND assertionName are CFStringRef in IOPMLib.h — not raw C strings.
    // Callers wrap kIOPMAssertPreventUserIdleSystemSleep with CoreFoundation.CFStr(...) and
    // CFRelease the result in a finally, just like assertionName.
    [DllImport(Lib)]
    internal static extern int IOPMAssertionCreateWithName(
        IntPtr assertionType, uint assertionLevel,
        IntPtr assertionName, out uint assertionID);

    [DllImport(Lib)]
    internal static extern int IOPMAssertionRelease(uint assertionID);

    // Power source / sleep notifications. Kept as a delegate; callers GC-root
    // the instance and pass Marshal.GetFunctionPointerForDelegate(...) as IntPtr.
    internal delegate void IOServiceInterestCallback(IntPtr refcon, IntPtr service, uint messageType, IntPtr messageArgument);

    [DllImport(Lib)]
    internal static extern IntPtr IORegisterForSystemPower(IntPtr refcon, out IntPtr thePortRef,
        IntPtr callback, out IntPtr notifier);

    [DllImport(Lib)]
    internal static extern IntPtr IONotificationPortGetRunLoopSource(IntPtr notify);

    [DllImport(Lib)]
    internal static extern int IODeregisterForSystemPower(ref IntPtr notifier);

    // IODeregisterForSystemPower releases the notifier object but NOT the
    // notification port itself, and does NOT close the connection. Callers must
    // additionally call IONotificationPortDestroy(_notifyPort) and IOServiceClose(_rootPort)
    // on the same Stop() path to fully release IOKit handles.
    [DllImport(Lib)]
    internal static extern void IONotificationPortDestroy(IntPtr notify);

    [DllImport(Lib)]
    internal static extern int IOServiceClose(IntPtr connect);

    [DllImport(Lib)]
    internal static extern void IOAllowPowerChange(IntPtr kernPort, IntPtr notificationID);

    internal const string kIOPMAssertPreventUserIdleSystemSleep = "PreventUserIdleSystemSleep";
}
