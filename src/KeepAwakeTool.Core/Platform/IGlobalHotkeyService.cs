using System;

namespace KeepAwakeTool.Core.Platform;

public interface IGlobalHotkeyService
{
    bool TryRegister(Hotkey hotkey, Action onPressed);
    void Unregister();
}
