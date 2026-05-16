using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KeepAwakeTool.App.ViewModels;

namespace KeepAwakeTool.App.Views;

public partial class HotkeyTab : UserControl
{
    // Pure modifier Key values that must not be treated as the "main" key of a chord.
    private static readonly Key[] ModifierKeys =
    [
        Key.LeftCtrl, Key.RightCtrl,
        Key.LeftAlt,  Key.RightAlt,
        Key.LeftShift, Key.RightShift,
        Key.LWin,     Key.RWin,
    ];

    private bool _capturing;
    private string? _pendingChord;     // formatted chord while keys are held
    private Key _pendingMainKey;       // the non-modifier key that was pressed
    private string _previousCombination = string.Empty;

    private Button? _recordButton;

    public HotkeyTab()
    {
        InitializeComponent();

        _recordButton = this.FindControl<Button>("RecordButton");
        if (_recordButton is not null)
            _recordButton.Click += OnRecordClick;

        // Tunnel so we capture before default handling eats the event.
        this.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        this.AddHandler(KeyUpEvent,   OnKeyUp,   RoutingStrategies.Tunnel);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // ──────────────────────────────────────────────────────────────────────
    // Record button
    // ──────────────────────────────────────────────────────────────────────

    private void OnRecordClick(object? sender, RoutedEventArgs e)
    {
        if (_capturing)
        {
            ExitCapture(commit: false);
            return;
        }

        var vm = HotkeyVm();
        if (vm is null) return;

        _previousCombination = vm.Combination;
        _pendingChord = null;
        _pendingMainKey = Key.None;
        _capturing = true;

        if (_recordButton is not null)
            _recordButton.Content = "Press keys…";

        vm.ValidationMessage = string.Empty;

        // Grab focus so keyboard events reach this control.
        _recordButton?.Focus();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Key capture
    // ──────────────────────────────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_capturing) return;

        // Escape cancels capture and restores the previous combination.
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            ExitCapture(commit: false);
            e.Handled = true;
            return;
        }

        e.Handled = true; // always consume while capturing

        // Skip pure modifier key-presses — wait for the main key.
        if (IsModifierKey(e.Key)) return;

        var vm = HotkeyVm();
        if (vm is null) return;

        var chord = HotkeyTabViewModel.FormatChord(e.KeyModifiers, e.Key);
        if (chord is not null)
        {
            _pendingChord   = chord;
            _pendingMainKey = e.Key;
            vm.Combination  = chord;       // live preview
            vm.ValidationMessage = string.Empty;
        }
        else
        {
            // Non-modifier key pressed but no valid chord (e.g. no modifier held,
            // or unmappable key like PrintScreen).
            bool hasModifier =
                e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                e.KeyModifiers.HasFlag(KeyModifiers.Alt)     ||
                e.KeyModifiers.HasFlag(KeyModifiers.Shift)   ||
                e.KeyModifiers.HasFlag(KeyModifiers.Meta);

            vm.ValidationMessage = hasModifier
                ? "Key not supported as hotkey target"
                : "Add a modifier (Ctrl, Alt, Shift or Win)";
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_capturing) return;

        e.Handled = true;

        // We finalize when the main non-modifier key is released.
        if (e.Key == _pendingMainKey && _pendingChord is not null)
        {
            ExitCapture(commit: true);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private void ExitCapture(bool commit)
    {
        _capturing = false;
        if (_recordButton is not null)
            _recordButton.Content = "Record…";

        var vm = HotkeyVm();
        if (vm is null) return;

        if (commit && _pendingChord is not null)
        {
            vm.Combination       = _pendingChord;
            vm.ValidationMessage = string.Empty;
        }
        else
        {
            vm.Combination       = _previousCombination;
            vm.ValidationMessage = commit ? "No valid combination captured" : string.Empty;
        }

        _pendingChord   = null;
        _pendingMainKey = Key.None;
    }

    private HotkeyTabViewModel? HotkeyVm()
        => DataContext is SettingsViewModel svm ? svm.Hotkey : null;

    private static bool IsModifierKey(Key key)
    {
        foreach (var m in ModifierKeys)
            if (key == m) return true;
        return false;
    }
}
