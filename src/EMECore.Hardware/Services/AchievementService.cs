using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class AchievementService
{
    private readonly StellarBladeParser _stellarBladeParser;
    private readonly EldenRingParser _eldenRingParser;
    private readonly PalworldParser _palworldParser;
    private readonly GTAVParser _gtaVParser;
    private readonly BlackMythWukongParser _blackMythWukongParser;
    private readonly MonsterHunterWildsParser _monsterHunterWildsParser;
    private readonly SkyrimDedicatedParser _skyrimParser;
    private readonly ForzaHorizon6Parser _forzaParser;
    private readonly SteamStoreService _steamStore;

    public AchievementService()
    {
        _stellarBladeParser = new StellarBladeParser();
        _eldenRingParser = new EldenRingParser();
        _palworldParser = new PalworldParser();
        _gtaVParser = new GTAVParser();
        _blackMythWukongParser = new BlackMythWukongParser();
        _monsterHunterWildsParser = new MonsterHunterWildsParser();
        _skyrimParser = new SkyrimDedicatedParser();
        _forzaParser = new ForzaHorizon6Parser();
        _steamStore = new SteamStoreService();
    }

    public async Task<List<Achievement>> GetAchievementsAsync(Game game)
    {
        if (IsStellarBlade(game))
            return _stellarBladeParser.ParseAchievements();

        if (IsEldenRing(game))
            return _eldenRingParser.ParseAchievements();

        if (IsPalworld(game))
            return _palworldParser.ParseAchievements();

        if (IsGTAV(game))
            return _gtaVParser.ParseAchievements();

        if (IsBlackMythWukong(game))
            return _blackMythWukongParser.ParseAchievements();

        if (IsMonsterHunterWilds(game))
            return _monsterHunterWildsParser.ParseAchievements();

        if (IsSkyrim(game))
            return _skyrimParser.ParseAchievements();

        if (IsForzaHorizon6(game))
            return _forzaParser.ParseAchievements();

        if (!string.IsNullOrEmpty(game.SteamAppId))
            return await GetSteamAchievementsAsync(game.SteamAppId);

        return new List<Achievement>();
    }

    public bool HasParserFor(Game game)
    {
        return IsStellarBlade(game) || IsEldenRing(game) || IsPalworld(game) ||
               IsGTAV(game) || IsBlackMythWukong(game) || IsMonsterHunterWilds(game) ||
               IsSkyrim(game) || IsForzaHorizon6(game);
    }

    public string? GetSavePath(Game game)
    {
        if (IsStellarBlade(game)) return _stellarBladeParser.FindSavePath();
        if (IsEldenRing(game)) return _eldenRingParser.FindSavePath();
        if (IsPalworld(game)) return _palworldParser.FindSavePath();
        if (IsGTAV(game)) return _gtaVParser.FindSavePath();
        if (IsBlackMythWukong(game)) return _blackMythWukongParser.FindSavePath();
        if (IsMonsterHunterWilds(game)) return _monsterHunterWildsParser.FindSavePath();
        if (IsSkyrim(game)) return _skyrimParser.FindSavePath();
        if (IsForzaHorizon6(game)) return _forzaParser.FindSavePath();
        return null;
    }

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string SteamApiKey = "B48A34F0A093B6CB53618DCC9F0640BE";

    private async Task<List<Achievement>> GetSteamAchievementsAsync(string appId)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={SteamApiKey}&appid={appId}";
            var json = await _http.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("game", out var game)) return new();
            if (!game.TryGetProperty("availableGameStats", out var stats)) return new();
            if (!stats.TryGetProperty("achievements", out var achArr)) return new();

            var achievements = new List<Achievement>();
            foreach (var ach in achArr.EnumerateArray())
            {
                var apiName = ach.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var displayName = ach.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : apiName;
                var description = ach.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
                var icon = ach.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "" : "";
                var iconGray = ach.TryGetProperty("icongray", out var gc) ? gc.GetString() ?? "" : "";

                achievements.Add(new Achievement
                {
                    GameId = appId,
                    Apiname = apiName,
                    Name = displayName,
                    Description = description,
                    Icon = icon,
                    Icongray = iconGray,
                    Achieved = false
                });
            }

            return achievements;
        }
        catch
        {
            return new List<Achievement>();
        }
    }

    private static bool IsStellarBlade(Game game)
    {
        return game.SteamAppId == "3489700" ||
               game.Name.Contains("Stellar Blade", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEldenRing(Game game)
    {
        return game.SteamAppId == "1245620" ||
               game.Name.Contains("Elden Ring", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("elden", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPalworld(Game game)
    {
        return game.SteamAppId == "1623730" ||
               game.Name.Contains("Palworld", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGTAV(Game game)
    {
        return game.SteamAppId == "271590" ||
               game.Name.Contains("Grand Theft Auto", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("GTA V", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("GTA5", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlackMythWukong(Game game)
    {
        return game.SteamAppId == "2358720" ||
               game.Name.Contains("Black Myth", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("Wukong", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("b1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMonsterHunterWilds(Game game)
    {
        return game.SteamAppId == "2246340" ||
               game.Name.Contains("Monster Hunter Wilds", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("MHWilds", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSkyrim(Game game)
    {
        return game.SteamAppId == "72850" ||
               game.SteamAppId == "489830" ||
               game.SteamAppId == "611670" ||
               game.Name.Contains("Skyrim", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Skyrim", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForzaHorizon6(Game game)
    {
        return game.SteamAppId == "271590" ||
               game.Name.Contains("Forza Horizon 6", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("ForzaHorizon6", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("ForzaHorizon6", StringComparison.OrdinalIgnoreCase);
    }
}
