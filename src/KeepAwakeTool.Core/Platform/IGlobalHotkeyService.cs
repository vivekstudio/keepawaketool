using System;

namespace KeepAwakeTool.Core.Platform;

public interface IGlobalHotkeyService
{
    bool TryRegister(Hotkey hotkey, Action onPressed);
    void Unregister();

    /// <summary>
    /// Raised from the hotkey pump thread after <c>RegisterHotKey</c> completes.
    /// <c>true</c> = registration succeeded; <c>false</c> = failed (already in use or other Win32 error).
    /// Subscribers must marshal to the UI thread if they touch UI.
    /// </summary>
    event Action<bool>? RegistrationResult;
}
