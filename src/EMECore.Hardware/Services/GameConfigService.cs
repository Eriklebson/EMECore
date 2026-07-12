using System.Text.Json;

namespace EMECore.Hardware.Services;

public class GameConfigService
{
    private GameConfigRoot? _config;
    private readonly string _configPath;

    public GameConfigService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "game-config.json");
    }

    public GameConfigRoot LoadConfig()
    {
        if (_config != null) return _config;

        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JsonSerializer.Deserialize<GameConfigRoot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            }
        }
        catch { }

        _config ??= new GameConfigRoot();
        return _config;
    }

    public GameConfig? FindGameConfig(string gameName, string? steamAppId = null, string? executablePath = null)
    {
        var config = LoadConfig();

        foreach (var kvp in config.Games)
        {
            var gameConfig = kvp.Value;

            foreach (var pattern in gameConfig.NamePatterns)
            {
                if (gameName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return gameConfig;
            }

            if (!string.IsNullOrEmpty(steamAppId) && gameConfig.SteamAppIds.Contains(steamAppId))
                return gameConfig;

            if (!string.IsNullOrEmpty(executablePath))
            {
                foreach (var pattern in gameConfig.NamePatterns)
                {
                    if (executablePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        return gameConfig;
                }
            }
        }

        return null;
    }

    public List<string> ResolveSavePaths(GameConfig gameConfig, string? steamPath = null)
    {
        var paths = new List<string>();
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var steamCommon = "";
        var steamUserdata = "";
        if (!string.IsNullOrEmpty(steamPath))
        {
            steamCommon = Path.Combine(steamPath, "steamapps", "common");
            steamUserdata = Path.Combine(steamPath, "userdata");
        }
        else
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            steamCommon = Path.Combine(programFilesX86, "Steam", "steamapps", "common");
            if (!Directory.Exists(steamCommon))
                steamCommon = Path.Combine(programFiles, "Steam", "steamapps", "common");
            steamUserdata = Path.Combine(programFilesX86, "Steam", "userdata");
            if (!Directory.Exists(steamUserdata))
                steamUserdata = Path.Combine(programFiles, "Steam", "userdata");
        }

        foreach (var loc in gameConfig.SaveLocations)
        {
            var resolvedPath = loc.Type.ToLowerInvariant() switch
            {
                "mydocuments" => Path.Combine(documents, loc.Path),
                "localappdata" => Path.Combine(localAppData, loc.Path),
                "appdata" => Path.Combine(appData, loc.Path),
                "savedgames" => Path.Combine(userProfile, "Saved Games", loc.Path),
                "userprofile" => Path.Combine(userProfile, loc.Path),
                "steamcommon" => Path.Combine(steamCommon, loc.Path),
                "steamuserdata" => Path.Combine(steamUserdata, loc.Path),
                _ => ""
            };

            if (!string.IsNullOrEmpty(resolvedPath))
                paths.Add(resolvedPath);
        }

        return paths;
    }

    public string? FindLatestSave(GameConfig gameConfig, string? steamPath = null)
    {
        var paths = ResolveSavePaths(gameConfig, steamPath);
        string? latestSave = null;
        var latestTime = DateTime.MinValue;

        foreach (var savePath in paths)
        {
            if (!Directory.Exists(savePath)) continue;
            try
            {
                var pattern = gameConfig.SaveLocations.FirstOrDefault()?.Pattern ?? "*";
                var searchOption = gameConfig.SaveLocations.FirstOrDefault()?.Recursive == true
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                foreach (var file in Directory.GetFiles(savePath, pattern, searchOption))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime > latestTime && info.Length > 100)
                    {
                        latestTime = info.LastWriteTime;
                        latestSave = file;
                    }
                }
            }
            catch { }
        }

        return latestSave;
    }

    public bool HasSave(GameConfig gameConfig, string? steamPath = null)
    {
        return FindLatestSave(gameConfig, steamPath) != null;
    }

    public List<string> GetAllGameIds()
    {
        var config = LoadConfig();
        return config.Games.Keys.ToList();
    }

    public GameConfig? GetGameConfig(string gameId)
    {
        var config = LoadConfig();
        return config.Games.GetValueOrDefault(gameId);
    }
}
