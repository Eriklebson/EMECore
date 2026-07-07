using System.Text.Json;

namespace EMECore.Hardware.Services;

public class SteamAchievementImageService
{
    private static readonly HttpClient _http = new();
    private static readonly Dictionary<string, string> _imageCache = new();

    public async Task<string?> GetAchievementIconAsync(string appId, string achievementName, bool achieved)
    {
        var cacheKey = $"{appId}_{achievementName}_{achieved}";
        if (_imageCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            // URL da API Web da Steam para obter informações de conquistas
            var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key=&appid={appId}";
            
            // Por enquanto, vamos usar URLs padrão baseadas no appId
            // A Steam fornece imagens de conquistas em um formato padrão
            var iconUrl = $"https://cdn.akamai.steamcdn.com/community/images/apps/{appId}/{achievementName}.jpg";
            var iconGrayUrl = $"https://cdn.akamai.steamcdn.com/community/images/apps/{appId}/{achievementName}_gray.jpg";
            
            // Verificar se a imagem existe
            var response = await _http.GetAsync(achieved ? iconUrl : iconGrayUrl);
            if (response.IsSuccessStatusCode)
            {
                var result = achieved ? iconUrl : iconGrayUrl;
                _imageCache[cacheKey] = result;
                return result;
            }
            
            // Fallback: retornar URL da imagem principal
            _imageCache[cacheKey] = iconUrl;
            return iconUrl;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetAchievementIconFromStoreAsync(string appId, string achievementName)
    {
        try
        {
            // URL da loja da Steam para imagens de conquistas
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(appId, out var appData) &&
                appData.TryGetProperty("data", out var data))
            {
                // A API da Steam não fornece imagens de conquistas diretamente
                // Precisamos usar a API Web específica para conquistas
                return null;
            }
        }
        catch { }
        return null;
    }

    public static string GetDefaultAchievementIcon(bool achieved)
    {
        // Retornar ícone padrão baseado no estado da conquista
        return achieved 
            ? "ms-appx:///Assets/Achievements/achieved.png"
            : "ms-appx:///Assets/Achievements/locked.png";
    }
}
