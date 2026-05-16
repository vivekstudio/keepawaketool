using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using KeepAwakeTool.App.ViewModels;
using KeepAwakeTool.Core.Config;
using Microsoft.Extensions.DependencyInjection;

namespace KeepAwakeTool.App.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigurationStore _store;
    private readonly SettingsViewModel _vm;

    public SettingsWindow(IServiceProvider sp)
    {
        _store = sp.GetRequiredService<ConfigurationStore>();
        _vm = new SettingsViewModel(_store.Load());
        InitializeComponent();
        DataContext = _vm;

        this.FindControl<Button>("ApplyButton")!.Click += (_, _) => ApplyAndStay();
        this.FindControl<Button>("OkButton")!.Click    += (_, _) => { ApplyAndStay(); Close(); };
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ApplyAndStay()
    {
        var cfg = _vm.BuildConfig();
        _store.Save(cfg);

        if (Avalonia.Application.Current is KeepAwakeTool.App.App app)
        {
            app.ApplyAutostart(cfg);
            app.ApplyHotkey(cfg);
            app.ApplyTheme(cfg);
        }
    }
}
