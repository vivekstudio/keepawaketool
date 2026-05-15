using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const int HotkeyId = 1;
    private HiddenMessageWindow? _window;
    private Action? _callback;

    public bool TryRegister(Hotkey hotkey, Action onPressed)
    {
        Unregister();
        _window = new HiddenMessageWindow();
        _window.HotkeyPressed += id => { if (id == HotkeyId) _callback?.Invoke(); };
        _callback = onPressed;

        uint mods =
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl)  ? User32.MOD_CONTROL : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)   ? User32.MOD_ALT     : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift) ? User32.MOD_SHIFT   : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)   ? User32.MOD_WIN     : 0u);

        var vk = MapKey(hotkey.Key);
        if (vk == 0) { Dispose(); return false; }

        var ok = User32.RegisterHotKey(_window.Handle, HotkeyId, mods, vk);
        if (!ok) { Dispose(); return false; }
        return true;
    }

    public void Unregister()
    {
        if (_window is null) return;
        User32.UnregisterHotKey(_window.Handle, HotkeyId);
        _window.Dispose();
        _window = null;
        _callback = null;
    }

    public void Dispose() => Unregister();

    private static uint MapKey(string key) => key.ToUpperInvariant() switch
    {
        var k when k.Length == 1 && k[0] >= 'A' && k[0] <= 'Z' => k[0],
        var k when k.Length == 1 && k[0] >= '0' && k[0] <= '9' => k[0],
        "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
        "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
        "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
        "P" => 0x50,
        _ => 0u
    };
}
