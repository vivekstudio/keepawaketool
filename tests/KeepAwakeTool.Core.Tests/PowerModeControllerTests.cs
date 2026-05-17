using FluentAssertions;
using KeepAwakeTool.Core.Config;
using KeepAwakeTool.Core.Power;
using KeepAwakeTool.Core.Tests.Fakes;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class PowerModeControllerTests
{
    [Fact]
    public void Start_calls_KeepSystemAwake_true()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Start(ConfigDefaults.Default().Power);
        pm.KeepAwakeOnCalls.Should().Be(1);
    }

    [Fact]
    public void Stop_calls_KeepSystemAwake_false()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Start(ConfigDefaults.Default().Power);
        ctrl.Stop();
        pm.KeepAwakeOffCalls.Should().Be(1);
    }

    [Fact]
    public void ApplyConfig_idempotent_for_same_state()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Start(new PowerConfig { ForceDisplayOffAfterInjection = false, PowerSaveMode = false });
        ctrl.ApplyConfig(new PowerConfig { ForceDisplayOffAfterInjection = false, PowerSaveMode = false });
        pm.KeepAwakeOnCalls.Should().Be(1);
    }

    [Fact]
    public void Rearm_reasserts_KeepSystemAwake_when_started()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Start(ConfigDefaults.Default().Power);
        ctrl.Rearm();
        pm.KeepAwakeOnCalls.Should().Be(2);
    }

    [Fact]
    public void Rearm_is_noop_when_not_started()
    {
        var pm = new FakePowerManager();
        var ctrl = new PowerModeController(pm);
        ctrl.Rearm();
        pm.KeepAwakeOnCalls.Should().Be(0);
    }
}
