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
