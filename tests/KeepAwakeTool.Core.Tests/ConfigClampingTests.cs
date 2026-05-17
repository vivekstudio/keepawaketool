using FluentAssertions;
using KeepAwakeTool.Core.Activity;
using KeepAwakeTool.Core.Config;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ConfigClampingTests
{
    [Fact]
    public void IntervalSeconds_below_minimum_is_clamped_to_10()
    {
        var input = new AppConfig { Activity = new ActivityConfig { IntervalSeconds = 1 } };
        ConfigClamping.Sanitize(input).Activity.IntervalSeconds.Should().Be(10);
    }

    [Fact]
    public void IntervalSeconds_above_maximum_is_clamped_to_240()
    {
        var input = new AppConfig { Activity = new ActivityConfig { IntervalSeconds = 10_000 } };
        ConfigClamping.Sanitize(input).Activity.IntervalSeconds.Should().Be(240);
    }

    [Fact]
    public void JigglePixels_below_minimum_becomes_1()
    {
        var input = new AppConfig
        {
            Activity = new ActivityConfig { Mouse = new MouseConfig { JigglePixels = 0 } }
        };
        ConfigClamping.Sanitize(input).Activity.Mouse.JigglePixels.Should().Be(1);
    }

    [Fact]
    public void EveryNthCycle_below_minimum_becomes_1()
    {
        var input = new AppConfig
        {
            Activity = new ActivityConfig { Keystroke = new KeystrokeConfig { EveryNthCycle = 0 } }
        };
        ConfigClamping.Sanitize(input).Activity.Keystroke.EveryNthCycle.Should().Be(1);
    }

    [Fact]
    public void Valid_config_is_unchanged()
    {
        var input = ConfigDefaults.Default();
        ConfigClamping.Sanitize(input).Should().Be(input);
    }
}
