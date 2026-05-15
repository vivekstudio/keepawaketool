using System;
using System.Linq;

namespace KeepAwakeTool.Core.Hotkey;

[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public static Hotkey Parse(string combination)
    {
        if (string.IsNullOrWhiteSpace(combination))
            throw new ArgumentException("Combination is empty", nameof(combination));

        var parts = combination.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var mods = HotkeyModifiers.None;
        string? key = null;

        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl": case "control":   mods |= HotkeyModifiers.Ctrl;  break;
                case "alt":                    mods |= HotkeyModifiers.Alt;   break;
                case "shift":                  mods |= HotkeyModifiers.Shift; break;
                case "win": case "windows":    mods |= HotkeyModifiers.Win;   break;
                default:
                    if (key is not null)
                        throw new ArgumentException($"Multiple non-modifier keys in '{combination}'");
                    key = p.ToUpperInvariant();
                    break;
            }
        }

        if (key is null) throw new ArgumentException($"No non-modifier key in '{combination}'");
        return new Hotkey(mods, key);
    }

    public override string ToString()
    {
        var parts = new[]
        {
            (Modifiers.HasFlag(HotkeyModifiers.Ctrl),  "Ctrl"),
            (Modifiers.HasFlag(HotkeyModifiers.Alt),   "Alt"),
            (Modifiers.HasFlag(HotkeyModifiers.Shift), "Shift"),
            (Modifiers.HasFlag(HotkeyModifiers.Win),   "Win"),
        }.Where(t => t.Item1).Select(t => t.Item2);
        return string.Join("+", parts.Append(Key));
    }
}
