using System.ComponentModel;
using FluentAssertions;
using KeepAwakeTool.App.ViewModels;
using KeepAwakeTool.Core.Config;
using Xunit;

namespace KeepAwakeTool.App.Tests;

public class ViewModelNotifyTests
{
    [Fact]
    public void HotkeyTab_Enabled_raises_PropertyChanged()
    {
        var vm = new HotkeyTabViewModel(ConfigDefaults.Default());
        string? changed = null;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed = e.PropertyName;
        vm.Enabled = !vm.Enabled;
        changed.Should().Be(nameof(HotkeyTabViewModel.Enabled));
    }

    [Fact]
    public void ScheduleTab_Enabled_raises_PropertyChanged()
    {
        var vm = new ScheduleTabViewModel(ConfigDefaults.Default());
        string? changed = null;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed = e.PropertyName;
        vm.Enabled = !vm.Enabled;
        changed.Should().Be(nameof(ScheduleTabViewModel.Enabled));
    }

    [Fact]
    public void ActivityTab_KeystrokeEnabled_raises_PropertyChanged()
    {
        var vm = new ActivityTabViewModel(ConfigDefaults.Default());
        string? changed = null;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed = e.PropertyName;
        vm.KeystrokeEnabled = !vm.KeystrokeEnabled;
        changed.Should().Be(nameof(ActivityTabViewModel.KeystrokeEnabled));
    }
}
