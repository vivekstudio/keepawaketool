using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class ScheduleTab : UserControl
{
    public ScheduleTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
