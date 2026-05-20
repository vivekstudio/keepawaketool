using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class ObjC
{
    private const string Lib = "/usr/lib/libobjc.A.dylib";

    [DllImport(Lib)]
    internal static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Lib)]
    internal static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Lib)]
    internal static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    // long-returning variant for SMAppServiceStatus
    [DllImport(Lib, EntryPoint = "objc_msgSend")]
    internal static extern long objc_msgSend_long(IntPtr receiver, IntPtr selector);

    internal static IntPtr Send(IntPtr r, string sel) => objc_msgSend(r, sel_registerName(sel));
    internal static IntPtr SendPtr(IntPtr r, string sel, IntPtr a) => objc_msgSend_ptr(r, sel_registerName(sel), a);
    internal static long SendLong(IntPtr r, string sel) => objc_msgSend_long(r, sel_registerName(sel));
}
