using System.Text.Json;
using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class SteamStoreService : ISteamStoreService
{
    private static readonly HttpClient _http = new();
    private static readonly Dictionary<string, (SteamStoreInfo info, DateTime cached)> _cache = new();
    private static readonly Dictionary<string, string> _searchCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> SearchAppIdAsync(string gameName)
    {
        if (_searchCache.TryGetValue(gameName, out var cached))
            return cached;

        try
        {
            var encoded = Uri.EscapeDataString(gameName);
            var url = $"https://store.steampowered.com/api/storesearch?term={encoded}&l=english&cc=us";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("items", out var items) &&
                items.GetArrayLength() > 0)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameEl))
                    {
                        var foundName = nameEl.GetString() ?? "";
                        if (IsMatch(gameName, foundName) && item.TryGetProperty("id", out var id))
                        {
                            var appId = id.GetInt32().ToString();
                            _searchCache[gameName] = appId;
                            return appId;
                        }
                    }
                }
            }
        }
        catch { }

        _searchCache[gameName] = string.Empty;
        return string.Empty;
    }

    private static bool IsMatch(string searchName, string foundName)
    {
        if (string.Equals(searchName, foundName, StringComparison.OrdinalIgnoreCase))
            return true;

        var sn = Normalize(searchName);
        var fn = Normalize(foundName);
        if (sn == fn) return true;

        var snWords = searchName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fnWords = foundName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (snWords.Length >= 2 && fnWords.Length >= 2)
        {
            var matchCount = snWords.Count(sw =>
                fnWords.Any(fw => string.Equals(sw, fw, StringComparison.OrdinalIgnoreCase)));
            if (matchCount >= 2 && matchCount >= snWords.Length * 0.7)
                return true;
        }

        return sn.Contains(fn) || fn.Contains(sn);
    }

    private static string Normalize(string name)
    {
        return new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLowerInvariant();
    }

    public async Task<SteamStoreInfo?> GetStoreInfoAsync(string appId)
    {
        if (_cache.TryGetValue(appId, out var cached) && (DateTime.UtcNow - cached.cached).TotalHours < 6)
            return cached.info;

        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(appId, out var appData) &&
                appData.TryGetProperty("data", out var data))
            {
                var info = new SteamStoreInfo
                {
                    Name = data.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    HeaderImage = data.TryGetProperty("header_image", out var h) ? h.GetString() ?? "" : "",
                    Description = data.TryGetProperty("short_description", out var d) ? d.GetString() ?? "" : ""
                };

                if (data.TryGetProperty("screenshots", out var screenshots) &&
                    screenshots.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ss in screenshots.EnumerateArray())
                    {
                        var full = ss.TryGetProperty("path_full", out var pf) ? pf.GetString() ?? "" : "";
                        var thumb = ss.TryGetProperty("path_thumbnail", out var pt) ? pt.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(full))
                            info.Screenshots.Add(new SteamScreenshot { PathFull = full, PathThumbnail = thumb });
                    }
                }

                // Buscar requisitos do sistema
                if (data.TryGetProperty("pc_requirements", out var pcReq))
                {
                    var requirements = new SteamRequirements();
                    
                    if (pcReq.TryGetProperty("minimum", out var min))
                        requirements.Minimum = min.GetString() ?? "";
                    
                    if (pcReq.TryGetProperty("recommended", out var rec))
                        requirements.Recommended = rec.GetString() ?? "";
                    
                    if (!string.IsNullOrEmpty(requirements.Minimum) || !string.IsNullOrEmpty(requirements.Recommended))
                        info.Requirements = requirements;
                }

                _cache[appId] = (info, DateTime.UtcNow);
                return info;
            }
        }
        catch { }
        return null;
    }
}
