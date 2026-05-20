using System;
using System.Runtime.InteropServices;

namespace KeepAwakeTool.Platform.Mac.Interop;

internal static class Carbon
{
    private const string Lib = "/System/Library/Frameworks/Carbon.framework/Carbon";

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventTypeSpec { public uint eventClass; public uint eventKind; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct EventHotKeyID { public uint signature; public uint id; }

    internal const uint kEventClassKeyboard = 0x6B657962;   // 'keyb'
    internal const uint kEventHotKeyPressed = 5;
    internal const uint typeEventHotKeyID = 0x686B6964;     // 'hkid'
    internal const uint kEventParamDirectObject = 0x2D2D2D2D; // '----'

    // Carbon modifier masks
    internal const uint cmdKey = 0x0100;
    internal const uint shiftKey = 0x0200;
    internal const uint optionKey = 0x0800;
    internal const uint controlKey = 0x1000;

    internal delegate int EventHandlerProcPtr(IntPtr inHandlerCallRef, IntPtr inEvent, IntPtr inUserData);

    [DllImport(Lib)]
    internal static extern int RegisterEventHotKey(uint inHotKeyCode, uint inHotKeyModifiers,
        EventHotKeyID inHotKeyID, IntPtr inTarget, uint inOptions, out IntPtr outRef);

    [DllImport(Lib)]
    internal static extern int UnregisterEventHotKey(IntPtr inHotKey);

    [DllImport(Lib)]
    internal static extern IntPtr GetApplicationEventTarget();

    [DllImport(Lib)]
    internal static extern int InstallEventHandler(IntPtr inTarget, EventHandlerProcPtr inHandler,
        int inNumTypes, [In] EventTypeSpec[] inList, IntPtr inUserData, out IntPtr outRef);

    [DllImport(Lib)]
    internal static extern int GetEventParameter(IntPtr inEvent, uint inName, uint inDesiredType,
        IntPtr outActualType, int inBufferSize, IntPtr outActualSize, out EventHotKeyID outData);
}
