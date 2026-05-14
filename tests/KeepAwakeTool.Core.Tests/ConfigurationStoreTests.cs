using System;
using System.IO;
using FluentAssertions;
using KeepAwakeTool.Core.Config;
using Xunit;

namespace KeepAwakeTool.Core.Tests;

public class ConfigurationStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _path;

    public ConfigurationStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "kat-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _path = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_writes_defaults_when_file_missing()
    {
        var store = new ConfigurationStore(_path);
        var loaded = store.Load();
        loaded.Should().Be(ConfigDefaults.Default());
        File.Exists(_path).Should().BeTrue();
    }

    [Fact]
    public void Load_returns_persisted_values()
    {
        File.WriteAllText(_path, """
            {
              "schemaVersion": 1,
              "activity": { "intervalSeconds": 90 }
            }
            """);
        var loaded = new ConfigurationStore(_path).Load();
        loaded.Activity.IntervalSeconds.Should().Be(90);
    }

    [Fact]
    public void Load_clamps_out_of_range_values()
    {
        File.WriteAllText(_path, """
            { "schemaVersion": 1, "activity": { "intervalSeconds": 5 } }
            """);
        new ConfigurationStore(_path).Load().Activity.IntervalSeconds.Should().Be(10);
    }
}
