using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class CoreGraphics
{
    private const string Lib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    // CGEventType
    internal const uint kCGEventMouseMoved = 5;
    // CGEventTapLocation
    internal const uint kCGHIDEventTap = 0;
    // CGEventSourceStateID
    internal const int kCGEventSourceStateHIDSystemState = 1;
    // CGEventType used by CGEventSourceSecondsSinceLastEventType for "any input"
    internal const uint kCGAnyInputEventType = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGPoint { public double X; public double Y; public CGPoint(double x, double y){X=x;Y=y;} }

    [DllImport(Lib)]
    internal static extern double CGEventSourceSecondsSinceLastEventType(int stateID, uint eventType);

    [DllImport(Lib)]
    internal static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [DllImport(Lib)]
    internal static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [DllImport(Lib)]
    internal static extern void CGEventPost(uint tap, IntPtr @event);

    [DllImport(Lib)]
    internal static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(Lib)]
    internal static extern CGPoint CGEventGetLocation(IntPtr @event);
}
