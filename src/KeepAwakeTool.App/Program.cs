using Avalonia;
using KeepAwakeTool.App.SingleInstance;
using System;
using System.IO;
using System.Threading.Tasks;

namespace KeepAwakeTool.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var logger = new KeepAwakeTool.Core.Diagnostics.FileLogger(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeepAwakeTool", "logs"));

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Log("FATAL", e.ExceptionObject?.ToString() ?? "unknown");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Log("ERROR", e.Exception.ToString());
            e.SetObserved();
        };

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
