using System.Text.Json;

namespace EMECore.Core.Services;

public class SettingsService
{
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMECore", "settings.json");

    private static AppSettings _settings = new();
    private static readonly object _lock = new();

    public static AppSettings Current
    {
        get
        {
            lock (_lock) return _settings;
        }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    public static void Set(string key, string value)
    {
        lock (_lock)
        {
            _settings.Values[key] = value;
            Save();
        }
    }

    public static string Get(string key, string defaultValue = "")
    {
        lock (_lock)
        {
            return _settings.Values.TryGetValue(key, out var val) ? val : defaultValue;
        }
    }
}

public class AppSettings
{
    public Dictionary<string, string> Values { get; set; } = new();
}
