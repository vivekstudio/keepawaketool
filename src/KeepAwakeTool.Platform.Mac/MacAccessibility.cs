using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public static class MacAccessibility
{
    private const string AppServices =
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CF =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(AppServices)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    [DllImport(CF)]
    private static extern IntPtr CFDictionaryCreate(IntPtr alloc, IntPtr[] keys, IntPtr[] values,
        long numValues, IntPtr keyCallBacks, IntPtr valueCallBacks);

    // Exported CFStringRef / CFBooleanRef data symbols (read once each).
    private static readonly Lazy<IntPtr> _promptKey = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(
            NativeLibrary.Load(AppServices), "kAXTrustedCheckOptionPrompt")));
    private static readonly Lazy<IntPtr> _cfTrue = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(CF), "kCFBooleanTrue")));

    /// <summary>Checks Accessibility trust WITHOUT prompting.</summary>
    public static bool IsTrusted() => AXIsProcessTrustedWithOptions(IntPtr.Zero);

    /// <summary>Checks trust and shows the system prompt if not yet trusted.</summary>
    public static bool RequestTrust()
    {
        var keys = new[] { _promptKey.Value };
        var vals = new[] { _cfTrue.Value };
        var dict = CFDictionaryCreate(IntPtr.Zero, keys, vals, 1, IntPtr.Zero, IntPtr.Zero);
        try { return AXIsProcessTrustedWithOptions(dict); }
        finally { if (dict != IntPtr.Zero) CoreFoundation.CFRelease(dict); }
    }
}
