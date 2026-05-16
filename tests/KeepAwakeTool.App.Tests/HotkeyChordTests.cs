using Avalonia.Input;
using FluentAssertions;
using KeepAwakeTool.App.ViewModels;
using Xunit;

namespace KeepAwakeTool.App.Tests;

public class HotkeyChordTests
{
    [Fact]
    public void Ctrl_Alt_letter_formats_canonically()
        => HotkeyTabViewModel.FormatChord(KeyModifiers.Control | KeyModifiers.Alt, Key.M)
            .Should().Be("Ctrl+Alt+M");

    [Fact]
    public void Digit_key_maps_to_number()
        => HotkeyTabViewModel.FormatChord(KeyModifiers.Control, Key.D5).Should().Be("Ctrl+5");

    [Fact]
    public void Function_key_preserved()
        => HotkeyTabViewModel.FormatChord(KeyModifiers.Alt, Key.F9).Should().Be("Alt+F9");

    [Fact]
    public void No_modifier_returns_null()
        => HotkeyTabViewModel.FormatChord(KeyModifiers.None, Key.M).Should().BeNull();

    [Fact]
    public void Non_hotkey_key_returns_null()
        => HotkeyTabViewModel.FormatChord(KeyModifiers.Control, Key.PrintScreen).Should().BeNull();
}
