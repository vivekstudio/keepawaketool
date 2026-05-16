using KeepAwakeTool.Core.Config;

namespace KeepAwakeTool.App.ViewModels;

public sealed class HotkeyTabViewModel : ObservableObject
{
    private bool _enabled;
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

    private string _combination = "Ctrl+Alt+P";
    public string Combination { get => _combination; set => SetField(ref _combination, value); }

    private string _validationMessage = string.Empty;
    public string ValidationMessage { get => _validationMessage; set => SetField(ref _validationMessage, value); }

    public HotkeyTabViewModel(AppConfig cfg)
    {
        Enabled = cfg.Hotkey.Enabled;
        Combination = cfg.Hotkey.Combination;
    }

    public HotkeyConfig Build() => new() { Enabled = Enabled, Combination = Combination };

    // Maps an Avalonia KeyModifiers + Key into the "Ctrl+Alt+P" string format,
    // or returns null if the key is not a valid standalone hotkey key.
    public static string? FormatChord(Avalonia.Input.KeyModifiers mods, Avalonia.Input.Key key)
    {
        string? main = key switch
        {
            >= Avalonia.Input.Key.A and <= Avalonia.Input.Key.Z => key.ToString(),                 // "A".."Z"
            >= Avalonia.Input.Key.D0 and <= Avalonia.Input.Key.D9 => ((int)(key - Avalonia.Input.Key.D0)).ToString(), // "0".."9"
            >= Avalonia.Input.Key.NumPad0 and <= Avalonia.Input.Key.NumPad9 => ((int)(key - Avalonia.Input.Key.NumPad0)).ToString(),
            >= Avalonia.Input.Key.F1 and <= Avalonia.Input.Key.F12 => key.ToString(),              // "F1".."F12"
            _ => null
        };
        if (main is null) return null;

        var parts = new System.Collections.Generic.List<string>(4);
        if (mods.HasFlag(Avalonia.Input.KeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(Avalonia.Input.KeyModifiers.Alt))     parts.Add("Alt");
        if (mods.HasFlag(Avalonia.Input.KeyModifiers.Shift))   parts.Add("Shift");
        if (mods.HasFlag(Avalonia.Input.KeyModifiers.Meta))    parts.Add("Win");
        if (parts.Count == 0) return null; // require at least one modifier for a global hotkey
        parts.Add(main);
        return string.Join("+", parts);
    }
}
