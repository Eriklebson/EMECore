using System.Text.Json;
using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class SteamStoreService : ISteamStoreService
{
    private static readonly HttpClient _http = new();
    private static readonly Dictionary<string, (SteamStoreInfo info, DateTime cached)> _cache = new();

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
