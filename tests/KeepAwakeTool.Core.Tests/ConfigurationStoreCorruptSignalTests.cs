using System;
using System.IO;
using FluentAssertions;
using KeepAwakeTool.Core.Config;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ConfigurationStoreCorruptSignalTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _path;

    public ConfigurationStoreCorruptSignalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "kat-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _path = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_corrupt_file_sets_LastCorruptBackupPath_and_raises_CorruptQuarantined_once()
    {
        File.WriteAllText(_path, "{ not valid json");

        var store = new ConfigurationStore(_path);

        var eventInvocations = 0;
        string? eventBackupPath = null;
        store.CorruptQuarantined += b =>
        {
            eventInvocations++;
            eventBackupPath = b;
        };

        _ = store.Load();

        store.LastCorruptBackupPath.Should().NotBeNull("a corrupt file must produce a backup path");
        store.LastCorruptBackupPath.Should().Contain(".corrupt.");
        store.LastCorruptBackupPath.Should().EndWith(".json");

        eventInvocations.Should().Be(1, "CorruptQuarantined should fire exactly once for one corrupt load");
        eventBackupPath.Should().Be(store.LastCorruptBackupPath);

        // Quarantined backup file must actually exist on disk
        File.Exists(store.LastCorruptBackupPath).Should().BeTrue("the corrupt file should have been moved to the backup path");
    }

    [Fact]
    public void Load_valid_file_leaves_LastCorruptBackupPath_null_and_does_not_raise_event()
    {
        File.WriteAllText(_path, """
            {
              "schemaVersion": 1,
              "activity": { "intervalSeconds": 30 }
            }
            """);

        var store = new ConfigurationStore(_path);

        var eventRaised = false;
        store.CorruptQuarantined += _ => eventRaised = true;

        _ = store.Load();

        store.LastCorruptBackupPath.Should().BeNull("no corruption means no backup path");
        eventRaised.Should().BeFalse("CorruptQuarantined must not fire for a valid config");
    }
}
