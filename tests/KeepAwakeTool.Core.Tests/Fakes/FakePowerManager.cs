using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakePowerManager : IPowerManager
{
    public int KeepAwakeOnCalls { get; private set; }
    public int KeepAwakeOffCalls { get; private set; }
    public int ForceDisplayOffCalls { get; private set; }
    public void KeepSystemAwake(bool on) { if (on) KeepAwakeOnCalls++; else KeepAwakeOffCalls++; }
    public void ForceDisplayOff() => ForceDisplayOffCalls++;
}
