using System.Text.Json;
using NusaLite.Infrastructure;

namespace NusaLite.Services;

public sealed class AppSettings
{
    public int WaterGoalMl { get; set; } = 2000;

    public int WaterReminderMinutes { get; set; } = 45;

    public int FocusMinutes { get; set; } = 25;

    public int BreakMinutes { get; set; } = 5;

    public int LyricOffsetMs { get; set; }

    public bool StartWithWindows { get; set; }

    public bool PinOpen { get; set; }

    public string WaterDate { get; set; } = string.Empty;

    public int WaterTodayMl { get; set; }

    public Dictionary<string, int> WaterHistory { get; set; } = new();
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public SettingsStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NusaLite");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");
        Load();
    }

    public AppSettings Current { get; private set; } = new();

    public string FilePath => _path;

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null)
                {
                    Current = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write("Settings could not be loaded.", ex);
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
        }
        catch (Exception ex)
        {
            Log.Write("Settings could not be saved.", ex);
        }
    }
}
