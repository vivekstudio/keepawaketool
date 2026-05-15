using System;
using System.IO;

namespace KeepAwakeTool.Core.Diagnostics;

public sealed class FileLogger
{
    private readonly object _gate = new();
    private readonly string _dir;

    public FileLogger(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    public void Log(string level, string message)
    {
        try
        {
            var path = Path.Combine(_dir, $"keepawaketool-{DateTime.UtcNow:yyyyMMdd}.log");
            var line = $"{DateTime.UtcNow:O} [{level}] {message}{Environment.NewLine}";
            lock (_gate) File.AppendAllText(path, line);
        }
        catch
        {
            // logger of last resort: never throw
        }
    }
}
