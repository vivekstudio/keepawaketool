using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KeepAwakeTool.App.Views;

public partial class GeneralTab : UserControl
{
    public GeneralTab() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
