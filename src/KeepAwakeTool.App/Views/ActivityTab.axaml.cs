using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class ActivityTab : UserControl
{
    public ActivityTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
