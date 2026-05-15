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

    [Fact]
    public void Load_quarantines_corrupt_file_and_returns_defaults()
    {
        File.WriteAllText(_path, "{ not valid json");
        var loaded = new ConfigurationStore(_path).Load();
        loaded.Should().Be(ConfigDefaults.Default());
        Directory.EnumerateFiles(_tempDir, "config.json.corrupt.*.json").Should().NotBeEmpty();
    }

    [Fact]
    public void Save_then_Load_round_trips_values()
    {
        var store = new ConfigurationStore(_path);
        var cfg = ConfigDefaults.Default() with
        {
            Power = new PowerConfig { ForceDisplayOffAfterInjection = true, PowerSaveMode = false }
        };
        store.Save(cfg);
        store.Load().Power.ForceDisplayOffAfterInjection.Should().BeTrue();
    }

    [Fact]
    public async Task Changed_event_fires_when_file_is_modified()
    {
        var store = new ConfigurationStore(_path);
        _ = store.Load();
        store.StartWatching();

        var tcs = new TaskCompletionSource<AppConfig>();
        store.Changed += (_, cfg) => tcs.TrySetResult(cfg);

        var updated = ConfigDefaults.Default() with
        {
            Activity = ConfigDefaults.Default().Activity with { IntervalSeconds = 120 }
        };
        store.Save(updated);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Activity.IntervalSeconds.Should().Be(120);

        store.StopWatching();
    }
}
