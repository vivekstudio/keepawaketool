using System;

namespace KeepAwakeTool.Platform.Win.Interop;

[Flags]
internal enum ExecutionState : uint
{
    Continuous      = 0x80000000,
    SystemRequired  = 0x00000001,
    DisplayRequired = 0x00000002,
    AwayModeRequired= 0x00000040
}
