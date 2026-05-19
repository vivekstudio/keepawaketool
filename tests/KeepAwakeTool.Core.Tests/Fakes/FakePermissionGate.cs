using KeepAwakeTool.Core.Platform;

namespace KeepAwakeTool.Core.Tests.Fakes;

public sealed class FakePermissionGate : IPermissionGate
{
    public bool CanInjectInput { get; set; } = true;
}
