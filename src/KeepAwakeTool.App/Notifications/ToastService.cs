using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace KeepAwakeTool.App.Notifications;

public sealed class ToastService
{
    /// <summary>
    /// Shows a brief toast notification near the bottom-right of the primary screen.
    /// Safe to call from any thread; marshals to the UI thread internally.
    /// Best-effort: any exception during display is silently swallowed.
    /// </summary>
    public void Show(string message)
    {
        if (Dispatcher.UIThread.CheckAccess()) ShowCore(message);
        else Dispatcher.UIThread.Post(() => ShowCore(message));
    }

    private static void ShowCore(string message)
    {
        try
        {
            var win = new Window
            {
                WindowDecorations = WindowDecorations.None,
                Topmost = true,
                ShowInTaskbar = false,
                CanResize = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Content = new Border
                {
                    Padding = new Avalonia.Thickness(16, 12),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = Brushes.White,
                        MaxWidth = 360,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            // Position in the Opened event so that ClientSize is valid after layout.
            win.Opened += (_, _) =>
            {
                try
                {
                    var screen = win.Screens?.Primary;
                    if (screen is not null)
                    {
                        var wa = screen.WorkingArea;
                        var w = (int)win.ClientSize.Width;
                        var h = (int)win.ClientSize.Height;
                        win.Position = new Avalonia.PixelPoint(
                            wa.X + wa.Width  - w - 24,
                            wa.Y + wa.Height - h - 24);
                    }
                }
                catch { /* best effort — wrong position is acceptable */ }
            };

            win.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            timer.Tick += (_, _) => { timer.Stop(); try { win.Close(); } catch { } };
            timer.Start();
        }
        catch
        {
            // best-effort: a failed toast must never crash the app (it's also logged elsewhere)
        }
    }
}
