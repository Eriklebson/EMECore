using System.Text.Json;

namespace EMECore.Hardware.Services;

public class AchievementImageService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Dictionary<string, string> _imageCache = new();
    private static readonly Dictionary<string, Dictionary<string, string>> _iconHashCache = new();
    private static readonly HashSet<string> _loadingSchemas = new();
    private static readonly string _imagesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMECore", "AchievementImages");

    private const string CdnBase = "https://shared.akamai.steamstatic.com/community_assets/images/apps";

    public AchievementImageService()
    {
        Directory.CreateDirectory(_imagesPath);
    }

    public async Task PreloadSchemaAsync(string appId)
    {
        if (_iconHashCache.ContainsKey(appId) || _loadingSchemas.Contains(appId))
            return;

        _loadingSchemas.Add(appId);
        try
        {
            await LoadStoreApiIconsAsync(appId);
        }
        finally
        {
            _loadingSchemas.Remove(appId);
        }
    }

    public async Task<string?> GetAchievementImageAsync(string appId, string achievementName, bool achieved)
    {
        var cacheKey = $"{appId}_{achievementName}_{achieved}";
        if (_imageCache.TryGetValue(cacheKey, out var cached) && File.Exists(cached))
            return cached;

        var gameDir = Path.Combine(_imagesPath, appId);
        Directory.CreateDirectory(gameDir);
        var safeName = string.Concat(achievementName.Where(c => char.IsLetterOrDigit(c) || c == '_'));
        if (string.IsNullOrEmpty(safeName)) safeName = "unknown";
        var suffix = achieved ? "a" : "g";
        var localPath = Path.Combine(gameDir, $"{safeName}_{suffix}.png");

        if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
        {
            _imageCache[cacheKey] = localPath;
            return localPath;
        }

        if (!_iconHashCache.ContainsKey(appId))
            await LoadStoreApiIconsAsync(appId);

        if (_iconHashCache.TryGetValue(appId, out var hashes) &&
            hashes.TryGetValue(achievementName, out var hash))
        {
            var url = $"{CdnBase}/{appId}/{hash}";
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

        return null;
    }

    public async Task<List<(string name, string iconHash, string localPath)>> DownloadAllHighlightedAsync(string appId)
    {
        var results = new List<(string, string, string)>();

        if (!_iconHashCache.ContainsKey(appId))
            await LoadStoreApiIconsAsync(appId);

        if (!_iconHashCache.TryGetValue(appId, out var hashes))
            return results;

        var gameDir = Path.Combine(_imagesPath, appId);
        Directory.CreateDirectory(gameDir);

        foreach (var (name, hash) in hashes)
        {
            var safeName = string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_'));
            if (string.IsNullOrEmpty(safeName)) continue;
            var localPath = Path.Combine(gameDir, $"{safeName}_highlighted.png");

            if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
            {
                results.Add((name, hash, localPath));
                continue;
            }

            var url = $"{CdnBase}/{appId}/{hash}";
            try
            {
                using var response = await _http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, bytes);
                    results.Add((name, hash, localPath));
                }
            }
            catch { }
        }

        return results;
    }

    private async Task LoadStoreApiIconsAsync(string appId)
    {
        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&filters=achievements";
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(appId, out var gameEl)) return;
            if (!gameEl.TryGetProperty("data", out var dataEl)) return;
            if (!dataEl.TryGetProperty("achievements", out var achObj)) return;

            var hashes = new Dictionary<string, string>();

            if (achObj.TryGetProperty("highlighted", out var highlighted))
            {
                foreach (var ach in highlighted.EnumerateArray())
                {
                    var name = ach.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var iconHash = ach.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(iconHash))
                        hashes[name] = iconHash;
                }
            }

            _iconHashCache[appId] = hashes;
        }
        catch { }
    }

    public static string GetFallbackIcon(bool achieved)
    {
        return achieved ? "\uE73E" : "\uE7C1";
    }
}
