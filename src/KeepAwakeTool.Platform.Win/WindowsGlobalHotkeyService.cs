using System;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Win.Interop;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const int HotkeyId = 1;
    private readonly Action<string, string>? _log;
    private HiddenMessageWindow? _window;
    private Action? _callback;
    private bool _registered;

    public WindowsGlobalHotkeyService(Action<string, string>? log = null) => _log = log;

    private HiddenMessageWindow EnsureWindow()
    {
        if (_window is null)
        {
            _window = new HiddenMessageWindow(_log);
            _window.HotkeyPressed += id => { if (id == HotkeyId) _callback?.Invoke(); };
        }
        return _window;
    }

    public bool TryRegister(Hotkey hotkey, Action onPressed)
    {
        var vk = MapKey(hotkey.Key);
        if (vk == 0) { _log?.Invoke("ERROR", $"Hotkey key not mappable: '{hotkey.Key}'"); return false; }

        uint mods =
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl)  ? User32.MOD_CONTROL : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)   ? User32.MOD_ALT     : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift) ? User32.MOD_SHIFT   : 0u) |
            (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)   ? User32.MOD_WIN     : 0u);

        _callback = onPressed;
        var win = EnsureWindow();
        if (_registered) win.RequestUnregister(HotkeyId);
        win.RequestRegister(HotkeyId, mods, vk);
        _registered = true;
        _log?.Invoke("INFO", $"Hotkey register requested mods=0x{mods:X} vk=0x{vk:X}");
        return true; // actual RegisterHotKey result is logged from the pump thread
    }

    public void Unregister()
    {
        if (_window is null || !_registered) return;
        _window.RequestUnregister(HotkeyId);
        _registered = false;
        _callback = null;
        _log?.Invoke("INFO", "Hotkey unregister requested");
    }

    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
        _registered = false;
        _callback = null;
    }

    private static uint MapKey(string key) => key.ToUpperInvariant() switch
    {
        var k when k.Length == 1 && k[0] >= 'A' && k[0] <= 'Z' => k[0],
        var k when k.Length == 1 && k[0] >= '0' && k[0] <= '9' => k[0],
        "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
        "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
        "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
        _ => 0u
    };
}
