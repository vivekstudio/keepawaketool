using System;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacInputSimulator : IInputSimulator
{
    private int _jiggleSign = 1;

    public void MoveMouse(MouseMode mode, int jigglePixels)
    {
        // Current cursor position via a throwaway event.
        var probe = CoreGraphics.CGEventCreate(IntPtr.Zero);
        var pos = CoreGraphics.CGEventGetLocation(probe);
        if (probe != IntPtr.Zero) CoreFoundation.CFRelease(probe);

        double dx = 0;
        if (mode == MouseMode.Jiggle)
        {
            dx = _jiggleSign * jigglePixels;
            _jiggleSign = -_jiggleSign;
        }

        var target = new CoreGraphics.CGPoint(pos.X + dx, pos.Y);
        var move = CoreGraphics.CGEventCreateMouseEvent(IntPtr.Zero,
            CoreGraphics.kCGEventMouseMoved, target, 0);
        if (move == IntPtr.Zero) return;
        CoreGraphics.CGEventPost(CoreGraphics.kCGHIDEventTap, move);
        CoreFoundation.CFRelease(move);
    }

    public void SendKey(VirtualKey key)
    {
        ushort code = key switch
        {
            VirtualKey.F13 => 105,
            VirtualKey.F14 => 107,
            VirtualKey.F15 => 113,
            _ => 113
        };
        var down = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, code, true);
        if (down != IntPtr.Zero)
        {
            CoreGraphics.CGEventPost(CoreGraphics.kCGHIDEventTap, down);
            CoreFoundation.CFRelease(down);
        }
        var up = CoreGraphics.CGEventCreateKeyboardEvent(IntPtr.Zero, code, false);
        if (up != IntPtr.Zero)
        {
            CoreGraphics.CGEventPost(CoreGraphics.kCGHIDEventTap, up);
            CoreFoundation.CFRelease(up);
        }
    }
}
