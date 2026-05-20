using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class CoreFoundation
{
    private const string Lib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(Lib)]
    internal static extern void CFRelease(IntPtr cf);

    [DllImport(Lib)]
    internal static extern IntPtr CFStringCreateWithCString(IntPtr alloc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cStr, uint encoding);

    internal const uint kCFStringEncodingUTF8 = 0x08000100;

    [DllImport(Lib)]
    internal static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(Lib)]
    internal static extern void CFRunLoopRun();

    [DllImport(Lib)]
    internal static extern void CFRunLoopStop(IntPtr rl);

    [DllImport(Lib)]
    internal static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    // kCFRunLoopCommonModes is an exported CFStringRef data symbol (read once).
    private static readonly Lazy<IntPtr> _commonModes = new(() =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(NativeLibrary.Load(Lib), "kCFRunLoopCommonModes")));
    internal static IntPtr kCFRunLoopCommonModes => _commonModes.Value;

    /// <summary>
    /// Creates a CFStringRef from a managed string (Create rule — caller MUST CFRelease
    /// the returned handle, typically in a finally block).
    /// </summary>
    internal static IntPtr CFStr(string s) =>
        CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8);
}
