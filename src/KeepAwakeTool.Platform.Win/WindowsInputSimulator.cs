using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsInputSimulator : IInputSimulator
{
    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    private int _jiggleSign = 1;

    public void MoveMouse(MouseMode mode, int jigglePixels)
    {
        if (mode == MouseMode.Invisible)
        {
            SendMouseMove(0, 0);
            return;
        }

        // Visible jiggle: a single relative hop that alternates direction on
        // each injection. A +N-then-(-N) round-trip in one call nets to zero
        // displacement, which Windows frequently coalesces and does NOT repaint
        // for an inactive/hidden idle cursor — so steady-state injections were
        // invisible. A single ±N move per injection always relocates the cursor
        // (forcing a redraw → reliably visible) while oscillating within
        // jigglePixels of the resting point, so it never drifts.
        SendMouseMove(_jiggleSign * jigglePixels, 0);
        _jiggleSign = -_jiggleSign;
    }

    public void SendKey(VirtualKey key)
    {
        ushort vk = key switch
        {
            VirtualKey.F13 => 0x7C,
            VirtualKey.F14 => 0x7D,
            VirtualKey.F15 => 0x7E,
            _ => 0x7E
        };

        var down = new INPUT
        {
            type = (uint)InputType.Keyboard,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KeyFlags.KeyDown } }
        };
        var up = new INPUT
        {
            type = (uint)InputType.Keyboard,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KeyFlags.KeyUp } }
        };
        User32.SendInput(2, new[] { down, up }, InputSize);
    }

    private static void SendMouseMove(int dx, int dy)
    {
        var inp = new INPUT
        {
            type = (uint)InputType.Mouse,
            U = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = MouseFlags.Move } }
        };
        User32.SendInput(1, new[] { inp }, InputSize);
    }
}
