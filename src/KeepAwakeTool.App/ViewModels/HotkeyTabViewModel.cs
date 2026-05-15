using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class HotkeyTabViewModel
{
    public bool Enabled { get; set; }
    public string Combination { get; set; } = "Ctrl+Alt+P";
    public string ValidationMessage { get; set; } = string.Empty;

    public HotkeyTabViewModel(AppConfig cfg)
    {
        Enabled = cfg.Hotkey.Enabled;
        Combination = cfg.Hotkey.Combination;
    }

    public HotkeyConfig Build() => new() { Enabled = Enabled, Combination = Combination };
}
