using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class AchievementService
{
    private readonly IDatabaseService _database;
    private readonly StellarBladeParser _stellarBladeParser;
    private readonly EldenRingParser _eldenRingParser;
    private readonly PalworldParser _palworldParser;
    private readonly GTAVParser _gtaVParser;
    private readonly BlackMythWukongParser _blackMythWukongParser;
    private readonly MonsterHunterWildsParser _monsterHunterWildsParser;
    private readonly SkyrimDedicatedParser _skyrimParser;
    private readonly ForzaHorizon6Parser _forzaParser;
    private readonly GodOfWarSaveParser _godOfWarParser;
    private readonly CyberpunkSaveParser _cyberpunkParser;
    private readonly DaysGoneSaveParser _daysGoneParser;
    private readonly HogwartsLegacySaveParser _hogwartsLegacyParser;
    private readonly Witcher3SaveParser _witcher3Parser;
    private readonly SteamStoreService _steamStore;
    private readonly GameConfigService _gameConfig;

    public AchievementService(IDatabaseService database)
    {
        _database = database;
        _stellarBladeParser = new StellarBladeParser();
        _eldenRingParser = new EldenRingParser();
        _palworldParser = new PalworldParser();
        _gtaVParser = new GTAVParser();
        _blackMythWukongParser = new BlackMythWukongParser();
        _monsterHunterWildsParser = new MonsterHunterWildsParser();
        _skyrimParser = new SkyrimDedicatedParser();
        _forzaParser = new ForzaHorizon6Parser();
        _godOfWarParser = new GodOfWarSaveParser();
        _cyberpunkParser = new CyberpunkSaveParser();
        _daysGoneParser = new DaysGoneSaveParser();
        _hogwartsLegacyParser = new HogwartsLegacySaveParser();
        _witcher3Parser = new Witcher3SaveParser();
        _steamStore = new SteamStoreService();
        _gameConfig = new GameConfigService();
    }

    public async Task<List<Achievement>> GetAchievementsAsync(Game game)
    {
        var achievements = new List<Achievement>();

        if (IsStellarBlade(game))
            achievements = _stellarBladeParser.ParseAchievements();
        else if (IsEldenRing(game))
            achievements = _eldenRingParser.ParseAchievements();
        else if (IsPalworld(game))
            achievements = _palworldParser.ParseAchievements();
        else if (IsGTAV(game))
            achievements = _gtaVParser.ParseAchievements();
        else if (IsBlackMythWukong(game))
            achievements = _blackMythWukongParser.ParseAchievements();
        else if (IsMonsterHunterWilds(game))
            achievements = _monsterHunterWildsParser.ParseAchievements();
        else if (IsSkyrim(game))
            achievements = _skyrimParser.ParseAchievements();
        else if (IsForzaHorizon6(game))
            achievements = _forzaParser.ParseAchievements();
        else if (IsGodOfWar(game))
            achievements = _godOfWarParser.ParseAchievements();
        else if (IsCyberpunk(game))
            achievements = _cyberpunkParser.ParseAchievements();
        else if (IsDaysGone(game))
            achievements = _daysGoneParser.ParseAchievements();
        else if (IsHogwartsLegacy(game))
            achievements = _hogwartsLegacyParser.ParseAchievements();
        else if (IsWitcher3(game))
            achievements = _witcher3Parser.ParseAchievements();
        else
        {
            var config = _gameConfig.FindGameConfig(game.Name, game.SteamAppId, game.ExecutablePath);
            if (config != null && config.ParserType != "none")
            {
                var savePath = _gameConfig.FindLatestSave(config);
                if (savePath != null)
                    achievements = await ParseWithGenericParser(game, config, savePath);
            }

            if (achievements.Count == 0 && !string.IsNullOrEmpty(game.SteamAppId))
                achievements = await GetSteamAchievementsAsync(game.SteamAppId);
        }

        if (achievements.Count > 0)
        {
            try { await _database.SaveAchievementsAsync(game.Id, achievements); } catch { }
            return achievements;
        }

        try
        {
            var saved = await _database.GetAchievementsAsync(game.Id);
            if (saved.Count > 0) return saved;
        }
        catch { }

        return new List<Achievement>();
    }

    public bool HasParserFor(Game game)
    {
        if (IsStellarBlade(game) || IsEldenRing(game) || IsPalworld(game) ||
            IsGTAV(game) || IsBlackMythWukong(game) || IsMonsterHunterWilds(game) ||
            IsSkyrim(game) || IsForzaHorizon6(game) || IsGodOfWar(game) || IsCyberpunk(game) ||
            IsDaysGone(game) || IsHogwartsLegacy(game) || IsWitcher3(game))
            return true;

        var config = _gameConfig.FindGameConfig(game.Name, game.SteamAppId, game.ExecutablePath);
        return config != null && config.ParserType != "none" && _gameConfig.HasSave(config);
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
        if (IsGodOfWar(game)) return _godOfWarParser.FindSavePath();
        if (IsCyberpunk(game)) return _cyberpunkParser.FindSavePath();
        if (IsDaysGone(game)) return _daysGoneParser.FindSavePath();
        if (IsHogwartsLegacy(game)) return _hogwartsLegacyParser.FindSavePath();
        if (IsWitcher3(game)) return _witcher3Parser.FindSavePath();

        var config = _gameConfig.FindGameConfig(game.Name, game.SteamAppId, game.ExecutablePath);
        if (config != null) return _gameConfig.FindLatestSave(config);
        return null;
    }

    public (string savePath, Func<List<Achievement>> parseFunc, string gameName)? GetSaveMonitorInfo(Game game)
    {
        if (IsStellarBlade(game))
            return (_stellarBladeParser.FindSavePath()!, () => _stellarBladeParser.ParseAchievements(), game.Name);
        if (IsEldenRing(game))
            return (_eldenRingParser.FindSavePath()!, () => _eldenRingParser.ParseAchievements(), game.Name);
        if (IsPalworld(game))
            return (_palworldParser.FindSavePath()!, () => _palworldParser.ParseAchievements(), game.Name);
        if (IsGTAV(game))
            return (_gtaVParser.FindSavePath()!, () => _gtaVParser.ParseAchievements(), game.Name);
        if (IsBlackMythWukong(game))
            return (_blackMythWukongParser.FindSavePath()!, () => _blackMythWukongParser.ParseAchievements(), game.Name);
        if (IsMonsterHunterWilds(game))
            return (_monsterHunterWildsParser.FindSavePath()!, () => _monsterHunterWildsParser.ParseAchievements(), game.Name);
        if (IsSkyrim(game))
            return (_skyrimParser.FindSavePath()!, () => _skyrimParser.ParseAchievements(), game.Name);
        if (IsForzaHorizon6(game))
            return (_forzaParser.FindSavePath()!, () => _forzaParser.ParseAchievements(), game.Name);
        if (IsGodOfWar(game))
            return (_godOfWarParser.FindSavePath()!, () => _godOfWarParser.ParseAchievements(), game.Name);
        if (IsCyberpunk(game))
            return (_cyberpunkParser.FindSavePath()!, () => _cyberpunkParser.ParseAchievements(), game.Name);
        if (IsDaysGone(game))
            return (_daysGoneParser.FindSavePath()!, () => _daysGoneParser.ParseAchievements(), game.Name);

        if (IsHogwartsLegacy(game))
            return (_hogwartsLegacyParser.FindSavePath()!, () => _hogwartsLegacyParser.ParseAchievements(), game.Name);

        if (IsWitcher3(game))
            return (_witcher3Parser.FindSavePath()!, () => _witcher3Parser.ParseAchievements(), game.Name);

        var config = _gameConfig.FindGameConfig(game.Name, game.SteamAppId, game.ExecutablePath);
        if (config != null)
        {
            var savePath = _gameConfig.FindLatestSave(config);
            if (savePath != null)
                return (savePath, () => ParseWithGenericParser(game, config, savePath).Result, game.Name);
        }
        return null;
    }

    private async Task<List<Achievement>> ParseWithGenericParser(Game game, GameConfig config, string savePath)
    {
        try
        {
            var parser = new SaveParserService();
            var saveFile = new SaveFile
            {
                FullPath = savePath,
                FileName = Path.GetFileName(savePath),
                DirectoryPath = Path.GetDirectoryName(savePath) ?? "",
                FileSize = new FileInfo(savePath).Length,
                LastModified = File.GetLastWriteTime(savePath)
            };

            var parsed = await parser.ParseAsync(saveFile);
            if (parsed.Count == 0) return new List<Achievement>();

            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "achievements", $"{config.AchievementDb}.json");
            if (!File.Exists(dbPath))
                return CreateGenericAchievements(game, parsed);

            var db = AchievementDatabase.LoadFromFile(dbPath);
            if (db == null || db.Achievements.Count == 0)
                return CreateGenericAchievements(game, parsed);

            var achievements = new List<Achievement>();
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateCondition(parsed, def.Condition);
                var progress = CalculateProgress(parsed, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }

            return achievements;
        }
        catch
        {
            return new List<Achievement>();
        }
    }

    private static bool EvaluateCondition(Dictionary<string, object> data, AchievementCondition condition)
    {
        return condition.Type switch
        {
            ConditionType.KeyExists => data.ContainsKey(condition.SaveKey),
            ConditionType.KeyValue => EvaluateKeyValue(data, condition),
            ConditionType.FlagSet => EvaluateKeyValue(data, condition),
            ConditionType.CounterReached => EvaluateKeyValue(data, condition),
            _ => false
        };
    }

    private static bool EvaluateKeyValue(Dictionary<string, object> data, AchievementCondition condition)
    {
        if (!TryGetNestedValue(data, condition.SaveKey, out var actualValue))
            return false;

        var actual = Convert.ToDouble(actualValue);
        var expected = Convert.ToDouble(condition.ExpectedValue ?? 0);

        return condition.Op switch
        {
            ConditionOperator.Equals => Math.Abs(actual - expected) < 0.001,
            ConditionOperator.GreaterOrEqual => actual >= expected,
            ConditionOperator.GreaterThan => actual > expected,
            ConditionOperator.LessThan => actual < expected,
            ConditionOperator.LessOrEqual => actual <= expected,
            _ => false
        };
    }

    private static bool TryGetNestedValue(Dictionary<string, object> data, string keyPath, out object? value)
    {
        value = null;
        var keys = keyPath.Split('.');
        object current = data;

        foreach (var key in keys)
        {
            if (current is Dictionary<string, object> dict && dict.TryGetValue(key, out var next))
            {
                current = next;
            }
            else if (current is System.Text.Json.JsonElement element)
            {
                if (element.ValueKind == System.Text.Json.JsonValueKind.Object && element.TryGetProperty(key, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        value = current;
        return true;
    }

    private static int CalculateProgress(Dictionary<string, object> data, AchievementDefinition def)
    {
        if (def.MaxProgress <= 0) return 0;
        if (!TryGetNestedValue(data, def.Condition.SaveKey, out var value)) return 0;

        try
        {
            var numVal = Convert.ToInt32(value);
            return Math.Min(numVal, def.MaxProgress);
        }
        catch { return 0; }
    }

    private static List<Achievement> CreateGenericAchievements(Game game, Dictionary<string, object> saveData)
    {
        var achievements = new List<Achievement>();
        var keys = FindInterestingKeys(saveData);

        foreach (var key in keys.Take(20))
        {
            TryGetNestedValue(saveData, key, out var value);
            achievements.Add(new Achievement
            {
                GameId = game.Id,
                Apiname = $"generic_{key.Replace('.', '_').Replace(' ', '_')}",
                Achieved = true,
                Unlocktime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Name = FormatKeyName(key),
                Description = $"Detectado no save: {value}",
                Icon = "",
                Icongray = ""
            });
        }

        return achievements;
    }

    private static List<string> FindInterestingKeys(Dictionary<string, object> data, string prefix = "", int depth = 0)
    {
        var keys = new List<string>();
        if (depth > 3) return keys;

        foreach (var kvp in data)
        {
            if (kvp.Key.StartsWith('_')) continue;
            var fullPath = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is Dictionary<string, object> nested)
                keys.AddRange(FindInterestingKeys(nested, fullPath, depth + 1));
            else
                keys.Add(fullPath);
        }

        return keys;
    }

    private static string FormatKeyName(string key)
    {
        return key.Replace('.', ' ').Replace('_', ' ')
            .Split(' ')
            .Select(word => word.Length > 0 ? char.ToUpper(word[0]) + word[1..] : "")
            .Aggregate((a, b) => $"{a} {b}");
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

    private static bool IsGodOfWar(Game game)
    {
        return game.SteamAppId == "1593500" ||
               game.SteamAppId == "2322010" ||
               game.Name.Contains("God of War", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("GoW", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCyberpunk(Game game)
    {
        return game.SteamAppId == "1091500" ||
               game.Name.Contains("Cyberpunk", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Cyberpunk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDaysGone(Game game)
    {
        return game.SteamAppId == "1098040" ||
               game.Name.Contains("Days Gone", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("DaysGone", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHogwartsLegacy(Game game)
    {
        return game.SteamAppId == "990080" ||
               game.Name.Contains("Hogwarts Legacy", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("HogwartsLegacy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWitcher3(Game game)
    {
        return game.SteamAppId == "292030" ||
               game.Name.Contains("The Witcher 3", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("Witcher 3", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Witcher3", StringComparison.OrdinalIgnoreCase);
    }
}
