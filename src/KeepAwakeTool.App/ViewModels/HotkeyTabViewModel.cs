using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class HotkeyTabViewModel
{
    private readonly AppConfig _src;
    public HotkeyTabViewModel(AppConfig cfg) => _src = cfg;
    public HotkeyConfig Build() => _src.Hotkey;
}
