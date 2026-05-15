using Avalonia;
using KeepAwakeTool.App.SingleInstance;
using System;

namespace KeepAwakeTool.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!SingleInstanceGuard.TryAcquire())
        {
            SingleInstanceGuard.SignalExistingInstance();
            return 0;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        finally
        {
            SingleInstanceGuard.Release();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
