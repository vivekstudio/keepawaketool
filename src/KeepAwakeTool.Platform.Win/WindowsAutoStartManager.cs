using System;
using Microsoft.Win32;
using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Platform.Win;

public sealed class WindowsAutoStartManager : IAutoStartManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeepAwakeTool";

    private readonly string _exePath;
    public WindowsAutoStartManager(string exePath) => _exePath = exePath;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) as string is { } s && string.Equals(s.Trim('"'), _exePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Cannot open Run key");
        key.SetValue(ValueName, $"\"{_exePath}\"", RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
