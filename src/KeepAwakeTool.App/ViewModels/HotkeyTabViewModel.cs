using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class HotkeyTabViewModel : ObservableObject
{
    private bool _enabled;
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }
    public string Combination { get; set; } = "Ctrl+Alt+P";
    public string ValidationMessage { get; set; } = string.Empty;

    public HotkeyTabViewModel(AppConfig cfg)
    {
        Enabled = cfg.Hotkey.Enabled;
        Combination = cfg.Hotkey.Combination;
    }

    public HotkeyConfig Build() => new() { Enabled = Enabled, Combination = Combination };
}
