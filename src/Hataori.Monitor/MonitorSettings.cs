using System.Text.Json;
using Hataori.Application.Localization;

namespace Hataori.Monitor;

/// <summary>ユーザー単位のMonitor設定です。Server設定とは独立しています。</summary>
internal sealed record MonitorSettings
{
    public bool CloseToTray { get; init; } = true;
    public bool StartMinimized { get; init; }
    public bool AutoUpdate { get; init; } = true;
    public int RefreshSeconds { get; init; } = 3;
    public string Language { get; init; } = DisplayLanguage.Japanese;

    public void Validate()
    {
        if (RefreshSeconds is < 1 or > 300 || Language is not (DisplayLanguage.Japanese or DisplayLanguage.English))
        {
            throw new InvalidDataException("Invalid Monitor language or refresh interval (1–300 seconds).");
        }
    }
}

/// <summary>設定を原子的に保存します。</summary>
internal sealed class MonitorSettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public string FilePath { get; } = Path.GetFullPath(path);

    public static string UserDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hataori", "Monitor");

    public MonitorSettings Load(string defaultLanguage)
    {
        if (!File.Exists(FilePath)) return new MonitorSettings { Language = defaultLanguage };
        var settings = JsonSerializer.Deserialize<MonitorSettings>(File.ReadAllText(FilePath), JsonOptions)
            ?? throw new InvalidDataException("Monitor settings cannot be null.");
        settings.Validate();
        return settings;
    }

    public void Save(MonitorSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
