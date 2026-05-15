using System;

namespace KeepAwakeTool.Core.Platform;

public interface IIdleMonitor
{
    TimeSpan TimeSinceLastUserInput();
}
