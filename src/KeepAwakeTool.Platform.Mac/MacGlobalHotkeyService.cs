using System;
using System.Runtime.InteropServices;
using KeepAwakeTool.Core.Platform;
using KeepAwakeTool.Platform.Mac.Interop;

namespace KeepAwakeTool.Platform.Mac;

public sealed class MacGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const uint Signature = 0x4B415448; // 'KATH'
    private const uint HotkeyId = 1;

    private readonly Action<string, string>? _log;
    private RunLoopThread? _loop;
    private Action? _callback;
    private IntPtr _hotKeyRef;
    private Carbon.EventHandlerProcPtr? _handler;   // GC-rooted
    private Hotkey? _pending;

    public event Action<bool>? RegistrationResult;

    public MacGlobalHotkeyService(Action<string, string>? log = null) => _log = log;

    public bool TryRegister(Hotkey hotkey, Action onPressed)
    {
        _callback = onPressed;
        _pending = hotkey;

        if (_loop is null)
        {
            _handler = HandleHotKey;
            _loop = new RunLoopThread("KAT-HotkeyPump", () =>
            {
                var spec = new Carbon.EventTypeSpec
                {
                    eventClass = Carbon.kEventClassKeyboard,
                    eventKind = Carbon.kEventHotKeyPressed
                };
                Carbon.InstallEventHandler(Carbon.GetApplicationEventTarget(), _handler!,
                    1, new[] { spec }, IntPtr.Zero, out _);
                DoRegister();
            });
            _loop.Start();
        }
        else
        {
            DoRegister();
        }
        return true; // real result arrives async via RegistrationResult
    }

    private void DoRegister()
    {
        if (_hotKeyRef != IntPtr.Zero)
        {
            Carbon.UnregisterEventHotKey(_hotKeyRef);
            _hotKeyRef = IntPtr.Zero;
        }
        if (_pending is null) { RegistrationResult?.Invoke(false); return; }

        var code = MapKey(_pending.Key);
        if (code == 0xFFFF)
        {
            _log?.Invoke("ERROR", $"Hotkey key not mappable: '{_pending.Key}'");
            RegistrationResult?.Invoke(false);
            return;
        }

        uint mods =
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Ctrl)  ? Carbon.controlKey : 0u) |
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Alt)   ? Carbon.optionKey  : 0u) |
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Shift) ? Carbon.shiftKey   : 0u) |
            (_pending.Modifiers.HasFlag(HotkeyModifiers.Win)   ? Carbon.cmdKey     : 0u);

        var id = new Carbon.EventHotKeyID { signature = Signature, id = HotkeyId };
        var rc = Carbon.RegisterEventHotKey(code, mods, id, Carbon.GetApplicationEventTarget(),
            0, out _hotKeyRef);
        var ok = rc == 0 && _hotKeyRef != IntPtr.Zero;
        _log?.Invoke(ok ? "INFO" : "ERROR", $"RegisterEventHotKey rc={rc}");
        RegistrationResult?.Invoke(ok);
    }

    private int HandleHotKey(IntPtr callRef, IntPtr evt, IntPtr userData)
    {
        var rc = Carbon.GetEventParameter(evt, Carbon.kEventParamDirectObject,
            Carbon.typeEventHotKeyID, IntPtr.Zero, Marshal.SizeOf<Carbon.EventHotKeyID>(),
            IntPtr.Zero, out var hkId);
        if (rc == 0 && hkId.signature == Signature && hkId.id == HotkeyId)
            _callback?.Invoke();
        return 0; // noErr
    }

    public void Unregister()
    {
        if (_hotKeyRef != IntPtr.Zero)
        {
            Carbon.UnregisterEventHotKey(_hotKeyRef);
            _hotKeyRef = IntPtr.Zero;
        }
        _callback = null;
        _pending = null;
    }

    public void Dispose()
    {
        Unregister();
        _loop?.Dispose();
        _loop = null;
        _handler = null;
    }

    // macOS virtual keycodes for the keys the Record UI can capture.
    private static ushort MapKey(string key) => key.ToUpperInvariant() switch
    {
        "A" => 0,  "S" => 1,  "D" => 2,  "F" => 3,  "H" => 4,  "G" => 5,
        "Z" => 6,  "X" => 7,  "C" => 8,  "V" => 9,  "B" => 11, "Q" => 12,
        "W" => 13, "E" => 14, "R" => 15, "Y" => 16, "T" => 17,
        "1" => 18, "2" => 19, "3" => 20, "4" => 21, "6" => 22, "5" => 23,
        "9" => 25, "7" => 26, "8" => 28, "0" => 29,
        "O" => 31, "U" => 32, "I" => 34, "P" => 35, "L" => 37, "J" => 38,
        "K" => 40, "N" => 45, "M" => 46,
        "F1" => 122, "F2" => 120, "F3" => 99,  "F4" => 118,
        "F5" => 96,  "F6" => 97,  "F7" => 98,  "F8" => 100,
        "F9" => 101, "F10" => 109, "F11" => 103, "F12" => 111,
        "F13" => 105, "F14" => 107, "F15" => 113,
        _ => 0xFFFF
    };
}
