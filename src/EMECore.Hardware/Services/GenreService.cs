using System.Text.Json;
using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class GenreService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private const string ApiKey = "56338309e00541ae8c5ccd712c62b58d";

    public async Task<string?> GetGenreAsync(string gameName)
    {
        if (_cache.TryGetValue(gameName, out var cached))
            return string.IsNullOrEmpty(cached) ? null : cached;

        try
        {
            var encoded = Uri.EscapeDataString(gameName);
            var url = $"https://api.rawg.io/api/games?key={ApiKey}&search={encoded}&page_size=1";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");

            if (results.GetArrayLength() == 0) { _cache[gameName] = ""; return null; }

            var item = results[0];
            var apiName = item.GetProperty("name").GetString() ?? "";
            if (!IsMatch(gameName, apiName)) { _cache[gameName] = ""; return null; }

            var genres = new List<string>();
            if (item.TryGetProperty("genres", out var gArr))
                foreach (var g in gArr.EnumerateArray())
                    if (g.TryGetProperty("name", out var gn))
                        genres.Add(gn.GetString() ?? "");

            var result = genres.Count > 0 ? genres[0] : ""; // Primary genre
            _cache[gameName] = result;
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch { _cache[gameName] = ""; return null; }
    }

    public async Task FetchGenresAsync(List<EMECore.Core.Models.Game> games)
    {
        foreach (var g in games)
        {
            if (!string.IsNullOrEmpty(g.Genre)) continue;
            var genre = await GetGenreAsync(g.Name);
            if (!string.IsNullOrEmpty(genre)) g.Genre = genre;
        }
    }

    private static bool IsMatch(string search, string found)
    {
        if (string.Equals(search, found, StringComparison.OrdinalIgnoreCase)) return true;
        var sn = new string(search.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var fn = new string(found.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return sn == fn || sn.Contains(fn) || fn.Contains(sn);
    }
}
