using System;
using KeepAwakeTool.Core.Hotkey;

namespace KeepAwakeTool.Core.Platform;

public interface IGlobalHotkeyService
{
    bool TryRegister(Hotkey.Hotkey hotkey, Action onPressed);
    void Unregister();
}
