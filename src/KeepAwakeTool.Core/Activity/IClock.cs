using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeepAwakeTool.Core.Activity;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset LocalNow { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
