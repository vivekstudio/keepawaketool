using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeepAwakeTool.Core.Config;

public sealed class ConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Intentional: no JsonNamingPolicy — preserves PascalCase enum values
        // ("Invisible", "F15", "Monday") matching the spec §8 example JSON.
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _path;
    private FileSystemWatcher? _watcher;
    public event EventHandler<AppConfig>? Changed;

    public void StartWatching()
    {
        if (_watcher is not null) return;
        var dir = System.IO.Path.GetDirectoryName(_path)!;
        var name = System.IO.Path.GetFileName(_path);
        _watcher = new FileSystemWatcher(dir)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        void FireChanged(object s, FileSystemEventArgs e)
        {
            // Match on the target config file name (handles File.Replace rename)
            var eventName = System.IO.Path.GetFileName(e.FullPath);
            if (!string.Equals(eventName, name, StringComparison.OrdinalIgnoreCase)) return;
            try { Changed?.Invoke(this, Load()); } catch { /* swallow; logged elsewhere */ }
        }
        _watcher.Changed += FireChanged;
        _watcher.Created += FireChanged;
        _watcher.Renamed += (s, e) =>
        {
            // File.Replace renames tmp → config.json; match on the new name
            var newName = System.IO.Path.GetFileName(e.FullPath);
            if (!string.Equals(newName, name, StringComparison.OrdinalIgnoreCase)) return;
            try { Changed?.Invoke(this, Load()); } catch { /* swallow; logged elsewhere */ }
        };
    }

    public void StopWatching()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public ConfigurationStore(string path) => _path = path;

    public string Path => _path;

    public AppConfig Load()
    {
        if (!File.Exists(_path))
        {
            var defaults = ConfigDefaults.Default();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var parsed = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? ConfigDefaults.Default();
            return ConfigClamping.Sanitize(parsed);
        }
        catch (JsonException)
        {
            QuarantineCorrupt();
            var defaults = ConfigDefaults.Default();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
        if (File.Exists(_path)) File.Replace(tmp, _path, destinationBackupFileName: null);
        else File.Move(tmp, _path);
    }

    private void QuarantineCorrupt()
    {
        var backup = $"{_path}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        try { File.Move(_path, backup); } catch { /* best effort */ }
    }
}
