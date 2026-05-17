using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KeepAwakeTool.Core.Diagnostics;
using Xunit;

namespace KeepAwakeTool.App.Tests;

public class EnginePumpTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "katt-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunTickSafely_logs_error_when_tick_throws()
    {
        var logger = new FileLogger(_dir);

        await EnginePump.RunTickSafelyAsync(
            _ => throw new InvalidOperationException("boom"), logger, CancellationToken.None);

        var logFile = Directory.GetFiles(_dir, "*.log").Should().ContainSingle().Subject;
        var contents = File.ReadAllText(logFile);
        contents.Should().Contain("[ERROR]");
        contents.Should().Contain("boom");
    }

    [Fact]
    public async Task RunTickSafely_does_not_log_when_tick_succeeds()
    {
        var logger = new FileLogger(_dir);

        await EnginePump.RunTickSafelyAsync(_ => Task.CompletedTask, logger, CancellationToken.None);

        Directory.GetFiles(_dir, "*.log").Should().BeEmpty();
    }

    [Fact]
    public async Task RunTickSafely_does_not_log_on_cancellation()
    {
        var logger = new FileLogger(_dir);

        await EnginePump.RunTickSafelyAsync(
            _ => throw new OperationCanceledException(), logger, CancellationToken.None);

        Directory.GetFiles(_dir, "*.log").Should().BeEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
