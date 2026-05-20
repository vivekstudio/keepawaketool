using System;
using FluentAssertions;
using KeepAwakeTool.Platform.Mac;
using Xunit;

namespace KeepAwakeTool.Platform.Mac.Tests;

[Trait("Category", "PlatformIntegration")]
public class MacPlatformIntegrationTests
{
    [Fact]
    public void IdleMonitor_returns_non_negative_timespan()
    {
        var idle = new MacIdleMonitor().TimeSinceLastUserInput();
        idle.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Accessibility_IsTrusted_does_not_throw()
    {
        var act = () => MacAccessibility.IsTrusted();
        act.Should().NotThrow();
    }

    [Fact]
    public void InputSimulator_invisible_move_does_not_throw()
    {
        var act = () => new MacInputSimulator()
            .MoveMouse(KeepAwakeTool.Core.Activity.MouseMode.Invisible, 1);
        act.Should().NotThrow();
    }
}
