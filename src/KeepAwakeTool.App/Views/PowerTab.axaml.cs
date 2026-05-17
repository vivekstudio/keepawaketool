using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class PowerTab : UserControl
{
    public PowerTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
