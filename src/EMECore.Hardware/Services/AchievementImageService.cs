using System.Text.Json;

namespace EMECore.Hardware.Services;

public class AchievementImageService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Dictionary<string, string> _imageCache = new();
    private static readonly Dictionary<string, Dictionary<string, (string icon, string iconGray)>> _schemaCache = new();
    private static readonly Dictionary<string, Dictionary<string, string>> _displayNameToApiName = new();
    private static readonly HashSet<string> _loadingSchemas = new();
    private static readonly string _imagesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMECore", "AchievementImages");

    private const string SteamApiKey = "B48A34F0A093B6CB53618DCC9F0640BE";

    public AchievementImageService()
    {
        Directory.CreateDirectory(_imagesPath);
    }

    public async Task PreloadSchemaAsync(string appId)
    {
        if (_schemaCache.ContainsKey(appId) || _loadingSchemas.Contains(appId))
            return;

        _loadingSchemas.Add(appId);
        try
        {
            await LoadSteamSchemaAsync(appId);
        }
        finally
        {
            _loadingSchemas.Remove(appId);
        }
    }

    public async Task<string?> GetAchievementImageAsync(string appId, string achievementName, bool achieved, string? gameName = null)
    {
        var folderName = SanitizeFolderName(gameName ?? appId);
        var suffix = achieved ? "a" : "g";
        var cacheKey = $"{folderName}_{achievementName}_{suffix}";
        if (_imageCache.TryGetValue(cacheKey, out var cached) && File.Exists(cached))
            return cached;

        var gameDir = Path.Combine(_imagesPath, folderName);
        Directory.CreateDirectory(gameDir);
        var safeName = string.Concat(achievementName.Where(c => char.IsLetterOrDigit(c) || c == '_'));
        if (string.IsNullOrEmpty(safeName)) safeName = "unknown";
        var localPath = Path.Combine(gameDir, $"{safeName}_{suffix}.png");

        if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
        {
            _imageCache[cacheKey] = localPath;
            return localPath;
        }

        if (!_schemaCache.ContainsKey(appId))
            await LoadSteamSchemaAsync(appId);

        if (_schemaCache.TryGetValue(appId, out var achData))
        {
            string? url = null;

            if (achData.TryGetValue(achievementName, out var urls))
            {
                url = achieved ? urls.icon : urls.iconGray;
            }
            else if (_displayNameToApiName.TryGetValue(appId, out var nameMap) &&
                     nameMap.TryGetValue(achievementName, out var apiName) &&
                     achData.TryGetValue(apiName, out var urls2))
            {
                url = achieved ? urls2.icon : urls2.iconGray;
            }

            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    using var response = await _http.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(localPath, bytes);
                        _imageCache[cacheKey] = localPath;
                        return localPath;
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
    }

    private async Task LoadSteamSchemaAsync(string appId)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={SteamApiKey}&appid={appId}";
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("game", out var game)) return;
            if (!game.TryGetProperty("availableGameStats", out var stats)) return;
            if (!stats.TryGetProperty("achievements", out var achArr)) return;

            var achData = new Dictionary<string, (string icon, string iconGray)>();
            var nameMap = new Dictionary<string, string>();

            foreach (var ach in achArr.EnumerateArray())
            {
                var apiName = ach.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var displayName = ach.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : apiName;
                var icon = ach.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "" : "";
                var iconGray = ach.TryGetProperty("icongray", out var gc) ? gc.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(apiName))
                {
                    achData[apiName] = (icon, iconGray);
                    if (!string.IsNullOrEmpty(displayName) && displayName != apiName)
                        nameMap[displayName] = apiName;
                }
            }

            _schemaCache[appId] = achData;
            _displayNameToApiName[appId] = nameMap;
        }
        catch { }
    }

    public static string GetFallbackIcon(bool achieved)
    {
        return achieved ? "\uE73E" : "\uE7C1";
    }
}
